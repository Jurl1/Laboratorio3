using System.Globalization;
using Lab3.Web.Data;
using Lab3.Web.Services.Tmdb;

namespace Lab3.Web.Services.Catalog;

/// <summary>Convierte respuestas crudas de TMDB a los DTOs del sitio.</summary>
public static class TmdbMapper
{
    private const int AnimationGenre = 16;
    private const int AnimeKeyword = 210024;

    /// <summary>
    /// Regla de PLAN.md §6 para el detalle: género 16 y (keyword 210024 o origen JP).
    /// </summary>
    public static bool IsAnimeDetail(TmdbItem item)
    {
        var genres = item.Genres?.Select(g => g.Id) ?? item.GenreIds ?? [];
        if (!genres.Contains(AnimationGenre)) return false;

        var keywords = item.Keywords?.Keywords ?? item.Keywords?.Results ?? [];
        return keywords.Any(k => k.Id == AnimeKeyword) || IsJapanese(item);
    }

    /// <summary>
    /// En listados no vienen keywords: género 16 y origen JP (o idioma original japonés).
    /// </summary>
    public static bool IsAnimeListItem(TmdbItem item) =>
        (item.GenreIds ?? []).Contains(AnimationGenre) && IsJapanese(item);

    private static bool IsJapanese(TmdbItem item) =>
        (item.OriginCountry?.Contains("JP") ?? false) || item.OriginalLanguage == "ja";

    /// <summary>Tipo de un resultado de /search/multi. Null para personas u otros.</summary>
    public static TmdbType? TypeOf(TmdbItem item) => item.MediaType switch
    {
        "movie" => TmdbType.Movie,
        "tv" => TmdbType.Tv,
        _ => null,
    };

    public static TitleCard ToCard(TmdbItem item, TmdbType type, bool? forceAnime = null) => new(
        type,
        item.Id,
        NameOf(item, type),
        YearOf(type == TmdbType.Movie ? item.ReleaseDate : item.FirstAirDate),
        item.PosterPath,
        Math.Round(item.VoteAverage, 1),
        forceAnime ?? IsAnimeListItem(item));

    public static TitleDetail ToDetail(TmdbItem item, TmdbType type, string watchRegion)
    {
        var providers = item.WatchProviders?.Results.GetValueOrDefault(watchRegion);

        return new TitleDetail(
            type,
            item.Id,
            NameOf(item, type),
            type == TmdbType.Movie ? item.OriginalTitle : item.OriginalName,
            YearOf(type == TmdbType.Movie ? item.ReleaseDate : item.FirstAirDate),
            NullIfEmpty(item.Overview),
            NullIfEmpty(item.Tagline),
            item.PosterPath,
            item.BackdropPath,
            item.Genres?.Select(g => g.Name).ToList() ?? [],
            type == TmdbType.Movie ? item.Runtime : item.EpisodeRunTime?.FirstOrDefault(),
            type == TmdbType.Tv ? item.NumberOfSeasons : null,
            NullIfEmpty(item.ImdbId) ?? NullIfEmpty(item.ExternalIds?.ImdbId),
            PickTrailer(item.Videos),
            Math.Round(item.VoteAverage, 1),
            item.VoteCount,
            IsAnimeDetail(item),
            item.Credits?.Cast.OrderBy(c => c.Order).Take(12)
                .Select(c => new CastMember(c.Name, NullIfEmpty(c.Character), c.ProfilePath)).ToList() ?? [],
            providers is null ? null : new WatchProviders(
                providers.Link,
                MapProviders(providers.Flatrate),
                MapProviders(providers.Rent),
                MapProviders(providers.Buy)),
            item.Similar?.Results.Take(12).Select(s => ToCard(s, type)).ToList() ?? []);
    }

    /// <summary>
    /// Del campo videos: site=YouTube y type=Trailer. Prioriza oficiales y en español.
    /// </summary>
    public static string? PickTrailer(TmdbVideos? videos) =>
        videos?.Results
            .Where(v => v.Site == "YouTube" && v.Type == "Trailer" && !string.IsNullOrWhiteSpace(v.Key))
            .OrderByDescending(v => v.Language == "es")
            .ThenByDescending(v => v.Official)
            .Select(v => v.Key)
            .FirstOrDefault();

    public static int? YearOf(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? y : null;

    private static string NameOf(TmdbItem item, TmdbType type) =>
        (type == TmdbType.Movie ? item.Title ?? item.OriginalTitle : item.Name ?? item.OriginalName) ?? "(sin título)";

    private static List<WatchProvider> MapProviders(List<TmdbProvider>? list) =>
        list?.OrderBy(p => p.DisplayPriority).Select(p => new WatchProvider(p.ProviderName, p.LogoPath)).ToList() ?? [];

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
