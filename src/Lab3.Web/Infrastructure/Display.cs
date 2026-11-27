using System.Security.Claims;
using Lab3.Web.Data;

namespace Lab3.Web.Infrastructure;

/// <summary>Helpers chicos para rutas y vistas.</summary>
public static class Display
{
    private const string ImageBase = "https://image.tmdb.org/t/p/";

    /// <summary>Segmento de URL: /titulo/pelicula/603 o /titulo/serie/1396.</summary>
    public static string Slug(this TmdbType type) => type == TmdbType.Movie ? "pelicula" : "serie";

    public static TmdbType? ParseSlug(string? slug) => slug switch
    {
        "pelicula" => TmdbType.Movie,
        "serie" => TmdbType.Tv,
        _ => null,
    };

    public static string TitleUrl(TmdbType type, int tmdbId) => $"/titulo/{type.Slug()}/{tmdbId}";

    public static string TypeLabel(TmdbType type, bool isAnime) =>
        isAnime ? "Anime" : type == TmdbType.Movie ? "Película" : "Serie";

    public static string? Image(string? path, string size = "w342") =>
        string.IsNullOrEmpty(path) ? null : ImageBase + size + path;

    public static string StatusLabel(BookmarkStatus status) => status switch
    {
        BookmarkStatus.Pendiente => "Pendiente",
        BookmarkStatus.Viendo => "Viendo",
        BookmarkStatus.Visto => "Visto",
        BookmarkStatus.Abandonado => "Abandonado",
        _ => status.ToString(),
    };

    public static string Date(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ArgentinaTz).ToString("d 'de' MMMM yyyy");

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var s = parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1],
            _ => $"{parts[0][0]}{parts[^1][0]}",
        };
        return s.ToUpperInvariant();
    }

    public static string? UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static readonly TimeZoneInfo ArgentinaTz = FindTz();

    private static TimeZoneInfo FindTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
