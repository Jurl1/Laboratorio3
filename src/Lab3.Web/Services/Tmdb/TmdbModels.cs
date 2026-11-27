using System.Text.Json.Serialization;

namespace Lab3.Web.Services.Tmdb;

// Respuestas crudas de TMDB (API v3). Se deserializan con snake_case.
// Un mismo TmdbItem sirve para películas y series: los campos que no aplican quedan en null.

public sealed class TmdbPage
{
    public List<TmdbItem> Results { get; set; } = [];
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalResults { get; set; }
}

public sealed class TmdbItem
{
    public int Id { get; set; }
    public string? MediaType { get; set; }

    // Película: title / original_title / release_date. Serie: name / original_name / first_air_date.
    public string? Title { get; set; }
    public string? Name { get; set; }
    public string? OriginalTitle { get; set; }
    public string? OriginalName { get; set; }
    public string? ReleaseDate { get; set; }
    public string? FirstAirDate { get; set; }

    public string? Overview { get; set; }
    public string? Tagline { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public List<int>? GenreIds { get; set; }
    public List<TmdbGenre>? Genres { get; set; }
    public List<string>? OriginCountry { get; set; }
    public string? OriginalLanguage { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }

    // Solo detalle.
    public int? Runtime { get; set; }
    public List<int>? EpisodeRunTime { get; set; }
    public int? NumberOfSeasons { get; set; }
    public string? ImdbId { get; set; }

    // append_to_response
    public TmdbVideos? Videos { get; set; }
    public TmdbCredits? Credits { get; set; }
    [JsonPropertyName("watch/providers")]
    public TmdbWatchProviders? WatchProviders { get; set; }
    public TmdbPage? Similar { get; set; }
    public TmdbExternalIds? ExternalIds { get; set; }
    public TmdbKeywords? Keywords { get; set; }
}

public sealed class TmdbGenre
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class TmdbVideos
{
    public List<TmdbVideo> Results { get; set; } = [];
}

public sealed class TmdbVideo
{
    public string Key { get; set; } = "";
    public string Site { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Official { get; set; }
    [JsonPropertyName("iso_639_1")]
    public string? Language { get; set; }
}

public sealed class TmdbCredits
{
    public List<TmdbCast> Cast { get; set; } = [];
}

public sealed class TmdbCast
{
    public string Name { get; set; } = "";
    public string? Character { get; set; }
    public string? ProfilePath { get; set; }
    public int Order { get; set; }
}

public sealed class TmdbWatchProviders
{
    public Dictionary<string, TmdbCountryProviders> Results { get; set; } = [];
}

public sealed class TmdbCountryProviders
{
    public string? Link { get; set; }
    public List<TmdbProvider>? Flatrate { get; set; }
    public List<TmdbProvider>? Rent { get; set; }
    public List<TmdbProvider>? Buy { get; set; }
}

public sealed class TmdbProvider
{
    public string ProviderName { get; set; } = "";
    public string? LogoPath { get; set; }
    public int DisplayPriority { get; set; }
}

public sealed class TmdbExternalIds
{
    public string? ImdbId { get; set; }
}

public sealed class TmdbKeywords
{
    // Películas devuelven "keywords", series devuelven "results".
    public List<TmdbGenre>? Keywords { get; set; }
    public List<TmdbGenre>? Results { get; set; }
}
