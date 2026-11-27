using System.Net;
using System.Text.RegularExpressions;
using Lab3.Web.Data;
using Lab3.Web.Services.Tmdb;
using Lab3.Web.Services.YouTube;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lab3.Tests.Infrastructure;

/// <summary>
/// Levanta la app completa en memoria con SQLite en lugar de Postgres
/// y clientes falsos de TMDB y YouTube (sin red, sin API keys).
/// </summary>
public sealed class LabFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeTmdb Tmdb { get; } = new();
    public FakeYouTube YouTube { get; } = new();

    public LabFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Host=no-se-usa");
        builder.UseSetting("RateLimit:AuthPerMinute", "1000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));

            services.RemoveAll<ITmdbClient>();
            services.AddSingleton<ITmdbClient>(Tmdb);
            services.RemoveAll<IYouTubeClient>();
            services.AddSingleton<IYouTubeClient>(YouTube);
        });
    }

    /// <summary>Cliente con cookies, sin seguir redirects, por HTTPS (las cookies son Secure).</summary>
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    public T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public static partial class HttpExtensions
{
    /// <summary>Lee el anti-forgery token del formulario o del meta tag de la página.</summary>
    public static async Task<string> GetTokenAsync(this HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var m = TokenInput().Match(html);
        if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value);
        m = TokenMeta().Match(html);
        if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value);
        throw new InvalidOperationException($"No hay anti-forgery token en {url}");
    }

    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string pageUrl, string postUrl, Dictionary<string, string> fields)
    {
        var token = await client.GetTokenAsync(pageUrl);
        var data = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token };
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(data));
    }

    public static async Task RegisterAsync(this HttpClient client, string user, string password = "clave-segura-123")
    {
        var res = await client.PostFormAsync("/cuenta/registro", "/cuenta/registro", new()
        {
            ["UserName"] = user,
            ["DisplayName"] = $"Nombre {user}",
            ["Email"] = $"{user}@test.local",
            ["Password"] = password,
            ["ConfirmPassword"] = password,
        });
        if (res.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"Registro falló: {(int)res.StatusCode}\n{await res.Content.ReadAsStringAsync()}");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenInput();

    [GeneratedRegex("<meta name=\"request-verification-token\" content=\"([^\"]+)\"")]
    private static partial Regex TokenMeta();
}
