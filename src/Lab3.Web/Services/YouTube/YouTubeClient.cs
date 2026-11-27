using System.Net;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;

namespace Lab3.Web.Services.YouTube;

public sealed class YouTubeOptions
{
    /// <summary>API key de YouTube Data API v3. Variable de entorno: YouTube__ApiKey.</summary>
    public string? ApiKey { get; set; }
}

public record YouTubeVideo(string Id, string Title, string Channel, DateTime? PublishedAt, long? Views, TimeSpan? Duration);

public interface IYouTubeClient
{
    bool IsConfigured { get; }
    /// <summary>search.list (100 unidades de cuota) + videos.list (1 unidad).</summary>
    Task<List<YouTubeVideo>> SearchAsync(string query, int max, CancellationToken ct = default);
}

public sealed class YouTubeUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class YouTubeClient(HttpClient http, IOptions<YouTubeOptions> options, ILogger<YouTubeClient> logger) : IYouTubeClient
{
    private readonly string? _key = options.Value.ApiKey;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_key);

    public async Task<List<YouTubeVideo>> SearchAsync(string query, int max, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new YouTubeUnavailableException("Falta configurar YouTube__ApiKey.");

        using var search = await GetJsonAsync(
            $"search?part=snippet&type=video&safeSearch=moderate&maxResults={max}&q={Uri.EscapeDataString(query)}", ct);

        var videos = new List<YouTubeVideo>();
        foreach (var item in search.RootElement.GetProperty("items").EnumerateArray())
        {
            var id = item.GetProperty("id").TryGetProperty("videoId", out var v) ? v.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            var snippet = item.GetProperty("snippet");
            videos.Add(new YouTubeVideo(
                id,
                // La API devuelve entidades HTML (&#39;). Razor vuelve a escapar al mostrar.
                WebUtility.HtmlDecode(snippet.GetProperty("title").GetString() ?? ""),
                WebUtility.HtmlDecode(snippet.GetProperty("channelTitle").GetString() ?? ""),
                snippet.TryGetProperty("publishedAt", out var p) && p.TryGetDateTime(out var date) ? date.ToUniversalTime() : null,
                null,
                null));
        }

        if (videos.Count == 0) return videos;

        // Vistas y duración: 1 unidad de cuota para todos los IDs juntos.
        using var details = await GetJsonAsync(
            $"videos?part=statistics,contentDetails&id={string.Join(',', videos.Select(x => x.Id))}", ct);
        var extra = new Dictionary<string, (long? views, TimeSpan? duration)>();
        foreach (var item in details.RootElement.GetProperty("items").EnumerateArray())
        {
            long? views = item.TryGetProperty("statistics", out var s) && s.TryGetProperty("viewCount", out var vc)
                          && long.TryParse(vc.GetString(), out var n) ? n : null;
            TimeSpan? duration = null;
            if (item.TryGetProperty("contentDetails", out var cd) && cd.TryGetProperty("duration", out var d))
            {
                try { duration = XmlConvert.ToTimeSpan(d.GetString() ?? ""); }
                catch (FormatException) { }
            }
            extra[item.GetProperty("id").GetString() ?? ""] = (views, duration);
        }

        return videos
            .Select(x => extra.TryGetValue(x.Id, out var e) ? x with { Views = e.views, Duration = e.duration } : x)
            .ToList();
    }

    private async Task<JsonDocument> GetJsonAsync(string relative, CancellationToken ct)
    {
        try
        {
            using var res = await http.GetAsync($"{relative}&key={Uri.EscapeDataString(_key!)}", ct);
            if (!res.IsSuccessStatusCode)
            {
                // No loguear la URL completa: lleva la API key.
                logger.LogWarning("YouTube devolvió {Status} para {Endpoint}", (int)res.StatusCode, relative.Split('?')[0]);
                throw new YouTubeUnavailableException($"YouTube devolvió {(int)res.StatusCode}.");
            }
            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("YouTube no respondió ({Error})", ex.GetType().Name);
            throw new YouTubeUnavailableException("YouTube no responde.", ex);
        }
    }
}
