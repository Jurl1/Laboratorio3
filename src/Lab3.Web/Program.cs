using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.WebEncoders;
using System.Threading.RateLimiting;
using Lab3.Web.Data;
using Lab3.Web.Infrastructure;
using Lab3.Web.Services;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;
using Lab3.Web.Services.YouTube;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var isDev = builder.Environment.IsDevelopment();

// ---------- Base de datos ----------
var connectionString = config.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__Default.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// ---------- Usuarios (Identity) — PLAN.md §8 ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredUniqueChars = 4;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.User.RequireUniqueEmail = true;
        // Solo caracteres seguros para la URL /usuario/{username}.
        o.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";
        // Verificación por mail: pendiente de confirmar (CLAUDE.md). Por ahora desactivada.
        o.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<SpanishIdentityErrorDescriber>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/cuenta/login";
    o.LogoutPath = "/cuenta/logout";
    o.AccessDeniedPath = "/cuenta/login";
    o.Cookie.Name = "lab3.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    // Secure en todo pedido HTTPS. En producción todo llega como HTTPS (X-Forwarded-Proto de Cloudflare);
    // "Always" rompía cualquier pedido HTTP directo al contenedor (ej: un curl desde el LXC).
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
    o.SlidingExpiration = true;
});

// Keys persistidas en un volumen: si no, cada rebuild desloguea a todos (PLAN.md §8).
var dpBuilder = builder.Services.AddDataProtection().SetApplicationName("Lab3");
var keysPath = config["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// ---------- MVC + anti-forgery ----------
builder.Services.AddAntiforgery(o =>
{
    // El JS de likes y bookmarks manda el token en este header.
    o.HeaderName = "RequestVerificationToken";
    o.Cookie.Name = "lab3.af";
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.Configure<CookieTempDataProviderOptions>(o =>
{
    o.Cookie.Name = "lab3.tmp";
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Tildes y ñ tal cual en el HTML (Razor sigue escapando <, >, &, comillas).
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddControllersWithViews(o =>
{
    // Anti-forgery en todo POST/PUT/DELETE, también los endpoints que usa fetch.
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
}).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Rate limiting en login y registro ----------
// 10 intentos por minuto por IP (la IP real llega por ForwardedHeaders).
var authPerMinute = config.GetValue("RateLimit:AuthPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimits.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ---------- Detrás de Cloudflare Tunnel ----------
// Solo cloudflared llega a la app por la red de Docker, así que se confía en cualquier proxy.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---------- APIs externas (las keys solo viven en el servidor) ----------
builder.Services.Configure<TmdbOptions>(config.GetSection("Tmdb"));
builder.Services.Configure<YouTubeOptions>(config.GetSection("YouTube"));
builder.Services.Configure<SmtpOptions>(config.GetSection("Smtp"));

builder.Services.AddHttpClient<ITmdbClient, TmdbClient>(c =>
{
    c.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<IYouTubeClient, YouTubeClient>(c =>
{
    c.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ApiCache>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<MyRatings>();

var app = builder.Build();

// ---------- Migraciones al arrancar (PLAN.md §10) ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsNpgsql())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated(); // Tests con SQLite.

    if (config.GetValue<bool>("Demo:Seed"))
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
}

// ---------- Pipeline ----------
app.UseForwardedHeaders();

var culture = new CultureInfo("es-AR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture],
});

if (!isDev)
{
    app.UseExceptionHandler("/error/500");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseSecurityHeaders(enableCsp: !isDev);

// Sin UseHttpsRedirection: Cloudflare termina TLS y fuerza HTTPS ("Always Use HTTPS").
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/healthz", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok("ok") : Results.Problem("db no disponible", statusCode: 503));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Para WebApplicationFactory en los tests.
public partial class Program;
