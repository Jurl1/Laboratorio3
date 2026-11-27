using Lab3.Web.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__Default.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddControllersWithViews();

// Detrás de Cloudflare Tunnel: leer el esquema (https) y la IP real del cliente.
// Solo cloudflared llega a la app por la red de Docker, así que se confía en cualquier proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Aplica las migraciones pendientes al arrancar (PLAN.md §10).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Sin UseHttpsRedirection: Cloudflare termina TLS y fuerza HTTPS ("Always Use HTTPS").
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/healthz", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok("ok") : Results.Problem("db no disponible", statusCode: 503));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
