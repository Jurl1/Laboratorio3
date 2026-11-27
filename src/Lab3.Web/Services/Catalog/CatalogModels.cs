using Lab3.Web.Data;

namespace Lab3.Web.Services.Catalog;

// DTOs normalizados que usan los controllers y las vistas. Se guardan como JSON en ApiCache.

public record TitleCard(TmdbType Type, int TmdbId, string Name, int? Year, string? PosterPath, double VoteAverage, bool IsAnime);

public record CastMember(string Name, string? Character, string? ProfilePath);

public record WatchProvider(string Name, string? LogoPath);

public record WatchProviders(string? Link, List<WatchProvider> Flatrate, List<WatchProvider> Rent, List<WatchProvider> Buy)
{
    public bool IsEmpty => Flatrate.Count == 0 && Rent.Count == 0 && Buy.Count == 0;
}

public record TitleDetail(
    TmdbType Type,
    int TmdbId,
    string Name,
    string? OriginalName,
    int? Year,
    string? Overview,
    string? Tagline,
    string? PosterPath,
    string? BackdropPath,
    List<string> Genres,
    int? RuntimeMinutes,
    int? Seasons,
    string? ImdbId,
    string? TrailerKey,
    double VoteAverage,
    int VoteCount,
    bool IsAnime,
    List<CastMember> Cast,
    WatchProviders? Providers,
    List<TitleCard> Similar);

public record HomeLists(List<TitleCard> TrendingMovies, List<TitleCard> TrendingTv, List<TitleCard> Anime);

public record SearchResults(List<TitleCard> Items, int Page, int TotalPages);
