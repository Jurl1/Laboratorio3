using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Lab3.Web.Data;
using Microsoft.Extensions.Options;

namespace Lab3.Web.Services.Tmdb;

public sealed class TmdbOptions
{
    /// <summary>API Read Access Token (v4 bearer). Variable de entorno: Tmdb__ReadAccessToken.</summary>
    public string? ReadAccessToken { get; set; }
    public string Language { get; set; } = "es-MX";
    /// <summary>País para "Dónde ver".</summary>
    public string WatchRegion { get; set; } = "AR";
}

public interface ITmdbClient
{
    Task<TmdbPage> GetTrendingAsync(TmdbType type, CancellationToken ct = default);
    Task<TmdbPage> DiscoverAnimeAsync(TmdbType type, CancellationToken ct = default);
    Task<TmdbPage> SearchMultiAsync(string query, int page, CancellationToken ct = default);
    /// <summary>Devuelve null si TMDB responde 404.</summary>
    Task<TmdbItem?> GetDetailAsync(TmdbType type, int id, CancellationToken ct = default);
}

/// <summary>El catálogo no se puede consultar (falta el token o TMDB falló).</summary>
public sealed class CatalogUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class TmdbClient(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger) : ITmdbClient
{
    public const string AnimeGenreId = "16";
    public const string AnimeKeywordId = "210024";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly TmdbOptions _opt = options.Value;

    public Task<TmdbPage> GetTrendingAsync(TmdbType type, CancellationToken ct = default) =>
        GetPageAsync($"trending/{Segment(type)}/week?language={_opt.Language}", ct);

    public Task<TmdbPage> DiscoverAnimeAsync(TmdbType type, CancellationToken ct = default) =>
        GetPageAsync($"discover/{Segment(type)}?with_genres={AnimeGenreId}&with_keywords={AnimeKeywordId}" +
                     $"&sort_by=popularity.desc&include_adult=false&language={_opt.Language}", ct);

    public Task<TmdbPage> SearchMultiAsync(string query, int page, CancellationToken ct = default) =>
        GetPageAsync($"search/multi?query={Uri.EscapeDataString(query)}&page={page}&include_adult=false&language={_opt.Language}", ct);

    public async Task<TmdbItem?> GetDetailAsync(TmdbType type, int id, CancellationToken ct = default)
    {
        var url = $"{Segment(type)}/{id}?language={_opt.Language}" +
                  "&append_to_response=videos,credits,watch/providers,similar,external_ids,keywords" +
                  "&include_video_language=es,en,null";
        return await GetAsync<TmdbItem>(url, ct, allowNotFound: true);
    }

    private async Task<TmdbPage> GetPageAsync(string url, CancellationToken ct) =>
        await GetAsync<TmdbPage>(url, ct, allowNotFound: false) ?? new TmdbPage();

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct, bool allowNotFound) where T : class
    {
        if (string.IsNullOrWhiteSpace(_opt.ReadAccessToken))
            throw new CatalogUnavailableException("Falta configurar Tmdb__ReadAccessToken.");

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ReadAccessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage res;
        try
        {
            res = await http.SendAsync(req, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "TMDB no respondió: {Url}", url);
            throw new CatalogUnavailableException("TMDB no responde.", ex);
        }

        using (res)
        {
            if (allowNotFound && res.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!res.IsSuccessStatusCode)
            {
                // Nunca loguear el token: solo la URL relativa y el código.
                logger.LogWarning("TMDB devolvió {Status} para {Url}", (int)res.StatusCode, url);
                throw new CatalogUnavailableException($"TMDB devolvió {(int)res.StatusCode}.");
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct);
        }
    }

    public static string Segment(TmdbType type) => type == TmdbType.Movie ? "movie" : "tv";
}
