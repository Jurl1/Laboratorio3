using Lab3.Web.Data;
using Lab3.Web.Services.Tmdb;
using Lab3.Web.Services.YouTube;

namespace Lab3.Tests.Infrastructure;

/// <summary>TMDB falso con datos fijos. Con Fail = true simula que TMDB no responde.</summary>
public sealed class FakeTmdb : ITmdbClient
{
    public bool Fail { get; set; }
    public int DetailCalls { get; private set; }

    public static TmdbItem Matrix() => new()
    {
        Id = 603,
        MediaType = "movie",
        Title = "Matrix",
        OriginalTitle = "The Matrix",
        ReleaseDate = "1999-03-30",
        Overview = "Un hacker descubre la verdad sobre su realidad.",
        PosterPath = "/matrix.jpg",
        BackdropPath = "/matrix-bg.jpg",
        GenreIds = [28, 878],
        Genres = [new() { Id = 28, Name = "Acción" }, new() { Id = 878, Name = "Ciencia ficción" }],
        OriginalLanguage = "en",
        VoteAverage = 8.22,
        VoteCount = 25000,
        Runtime = 136,
        Videos = new()
        {
            Results =
            [
                new() { Key = "teaser1", Site = "YouTube", Type = "Teaser", Language = "en" },
                new() { Key = "trailerEN", Site = "YouTube", Type = "Trailer", Official = true, Language = "en" },
                new() { Key = "trailerES", Site = "YouTube", Type = "Trailer", Official = false, Language = "es" },
            ],
        },
        Credits = new() { Cast = [new() { Name = "Keanu Reeves", Character = "Neo", Order = 0 }] },
        WatchProviders = new()
        {
            Results = new()
            {
                ["AR"] = new()
                {
                    Link = "https://www.themoviedb.org/movie/603/watch?locale=AR",
                    Flatrate = [new() { ProviderName = "Max", LogoPath = "/max.jpg" }],
                },
            },
        },
        ExternalIds = new() { ImdbId = "tt0133093" },
        Keywords = new() { Keywords = [new() { Id = 1, Name = "hacker" }] },
        Similar = new() { Results = [new() { Id = 604, Title = "Matrix Reloaded", ReleaseDate = "2003-05-15" }] },
    };

    public static TmdbItem AttackOnTitan() => new()
    {
        Id = 1429,
        MediaType = "tv",
        Name = "Ataque a los titanes",
        OriginalName = "進撃の巨人",
        FirstAirDate = "2013-04-07",
        GenreIds = [16, 10759],
        Genres = [new() { Id = 16, Name = "Animación" }],
        OriginCountry = ["JP"],
        OriginalLanguage = "ja",
        VoteAverage = 8.7,
        VoteCount = 7000,
        NumberOfSeasons = 4,
        EpisodeRunTime = [24],
        Keywords = new() { Results = [new() { Id = 210024, Name = "anime" }] },
        ExternalIds = new() { ImdbId = "tt2560140" },
    };

    public static TmdbItem BreakingBad() => new()
    {
        Id = 1396,
        MediaType = "tv",
        Name = "Breaking Bad",
        FirstAirDate = "2008-01-20",
        GenreIds = [18],
        Genres = [new() { Id = 18, Name = "Drama" }],
        OriginCountry = ["US"],
        OriginalLanguage = "en",
        VoteAverage = 8.9,
        VoteCount = 14000,
        NumberOfSeasons = 5,
    };

    public Task<TmdbPage> GetTrendingAsync(TmdbType type, CancellationToken ct = default)
    {
        Check();
        return Task.FromResult(new TmdbPage
        {
            Page = 1,
            TotalPages = 1,
            Results = type == TmdbType.Movie ? [Matrix()] : [BreakingBad()],
        });
    }

    public Task<TmdbPage> DiscoverAnimeAsync(TmdbType type, CancellationToken ct = default)
    {
        Check();
        return Task.FromResult(new TmdbPage { Page = 1, TotalPages = 1, Results = [AttackOnTitan()] });
    }

    public Task<TmdbPage> SearchMultiAsync(string query, int page, CancellationToken ct = default)
    {
        Check();
        return Task.FromResult(new TmdbPage
        {
            Page = page,
            TotalPages = 1,
            Results = [Matrix(), BreakingBad(), AttackOnTitan(), new TmdbItem { Id = 6384, MediaType = "person", Name = "Keanu Reeves" }],
        });
    }

    public Task<TmdbItem?> GetDetailAsync(TmdbType type, int id, CancellationToken ct = default)
    {
        Check();
        DetailCalls++;
        TmdbItem? item = (type, id) switch
        {
            (TmdbType.Movie, 603) => Matrix(),
            (TmdbType.Tv, 1429) => AttackOnTitan(),
            (TmdbType.Tv, 1396) => BreakingBad(),
            _ => null,
        };
        return Task.FromResult(item);
    }

    private void Check()
    {
        if (Fail) throw new CatalogUnavailableException("TMDB no responde.");
    }
}

public sealed class FakeYouTube : IYouTubeClient
{
    public int Calls { get; private set; }
    public bool IsConfigured => true;

    public Task<List<YouTubeVideo>> SearchAsync(string query, int max, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(new List<YouTubeVideo>
        {
            new("abcDEF12345", "Matrix: análisis <completo>", "Canal de cine", DateTime.UtcNow, 12345, TimeSpan.FromMinutes(14)),
        });
    }
}
