using System.Net;
using System.Text;
using Lab3.Web.Data;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lab3.Tests;

/// <summary>El cliente real de TMDB contra respuestas con el formato de la API v3 (sin red).</summary>
public class TmdbClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private static (TmdbClient client, StubHandler handler) Create(string json, HttpStatusCode status = HttpStatusCode.OK, string? token = "test-token")
    {
        var handler = new StubHandler(status, json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var opt = Options.Create(new TmdbOptions { ReadAccessToken = token, Language = "es-MX", WatchRegion = "AR" });
        return (new TmdbClient(http, opt, NullLogger<TmdbClient>.Instance), handler);
    }

    // Recorte de GET /tv/1429?append_to_response=videos,credits,watch/providers,similar,external_ids,keywords
    private const string TvDetailJson = """
    {
      "id": 1429,
      "name": "Ataque a los titanes",
      "original_name": "進撃の巨人",
      "first_air_date": "2013-04-07",
      "overview": "Hace siglos, los humanos casi fueron exterminados por titanes.",
      "poster_path": "/hTP1DtLGFamjfu8WqjnuQdP1n4i.jpg",
      "backdrop_path": "/rqbCbjB19amtOtFQbb3K2lgm2zv.jpg",
      "genres": [{ "id": 16, "name": "Animación" }, { "id": 10759, "name": "Action & Adventure" }],
      "origin_country": ["JP"],
      "original_language": "ja",
      "episode_run_time": [24],
      "number_of_seasons": 4,
      "vote_average": 8.664,
      "vote_count": 7123,
      "videos": { "results": [ { "iso_639_1": "es", "key": "abc123", "site": "YouTube", "type": "Trailer", "official": true } ] },
      "credits": { "cast": [ { "name": "Yuki Kaji", "character": "Eren Yeager (voice)", "profile_path": "/x.jpg", "order": 0 } ] },
      "watch/providers": { "results": { "AR": { "link": "https://www.themoviedb.org/tv/1429/watch?locale=AR", "flatrate": [ { "provider_name": "Crunchyroll", "logo_path": "/cr.jpg", "display_priority": 3 } ] } } },
      "similar": { "page": 1, "results": [ { "id": 37854, "name": "One Piece", "first_air_date": "1999-10-20", "genre_ids": [16], "origin_country": ["JP"] } ], "total_pages": 1, "total_results": 1 },
      "external_ids": { "imdb_id": "tt2560140" },
      "keywords": { "results": [ { "id": 210024, "name": "anime" } ] }
    }
    """;

    [Fact]
    public async Task Detalle_de_serie_se_deserializa_y_mapea()
    {
        var (client, handler) = Create(TvDetailJson);
        var item = await client.GetDetailAsync(TmdbType.Tv, 1429);

        Assert.Equal("Bearer", handler.Last!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", handler.Last.Headers.Authorization.Parameter);
        var url = handler.Last.RequestUri!.ToString();
        Assert.StartsWith("https://api.themoviedb.org/3/tv/1429?", url);
        Assert.Contains("append_to_response=videos,credits,watch/providers,similar,external_ids,keywords", url);

        var d = TmdbMapper.ToDetail(item!, TmdbType.Tv, "AR");
        Assert.Equal("Ataque a los titanes", d.Name);
        Assert.Equal("進撃の巨人", d.OriginalName);
        Assert.Equal(2013, d.Year);
        Assert.Equal(24, d.RuntimeMinutes);
        Assert.Equal(4, d.Seasons);
        Assert.Equal("tt2560140", d.ImdbId);
        Assert.Equal("abc123", d.TrailerKey);
        Assert.True(d.IsAnime);
        Assert.Equal(8.7, d.VoteAverage);
        Assert.Equal("Eren Yeager (voice)", Assert.Single(d.Cast).Character);
        Assert.Equal("Crunchyroll", Assert.Single(d.Providers!.Flatrate).Name);
        Assert.True(Assert.Single(d.Similar).IsAnime);
    }

    [Fact]
    public async Task Busqueda_multi_lee_media_type_y_genre_ids()
    {
        var (client, handler) = Create("""
        { "page": 1, "total_pages": 3, "total_results": 42, "results": [
          { "id": 603, "media_type": "movie", "title": "Matrix", "release_date": "1999-03-30", "genre_ids": [28, 878], "vote_average": 8.2 },
          { "id": 6384, "media_type": "person", "name": "Keanu Reeves" }
        ] }
        """);
        var page = await client.SearchMultiAsync("matrix & co", 2);

        Assert.Contains("query=matrix%20%26%20co", handler.Last!.RequestUri!.AbsoluteUri);
        Assert.Contains("page=2", handler.Last.RequestUri.Query);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal("movie", page.Results[0].MediaType);
        Assert.Equal([28, 878], page.Results[0].GenreIds);
        Assert.Null(TmdbMapper.TypeOf(page.Results[1]));
    }

    [Fact]
    public async Task Detalle_404_devuelve_null()
    {
        var (client, _) = Create("""{ "success": false, "status_code": 34 }""", HttpStatusCode.NotFound);
        Assert.Null(await client.GetDetailAsync(TmdbType.Movie, 1));
    }

    [Fact]
    public async Task Error_de_TMDB_o_falta_de_token_avisa_sin_romper()
    {
        var (unauthorized, _) = Create("""{ "status_code": 7 }""", HttpStatusCode.Unauthorized);
        await Assert.ThrowsAsync<CatalogUnavailableException>(() => unauthorized.GetTrendingAsync(TmdbType.Movie));

        var (noToken, handler) = Create("{}", token: null);
        await Assert.ThrowsAsync<CatalogUnavailableException>(() => noToken.GetTrendingAsync(TmdbType.Movie));
        Assert.Null(handler.Last); // Ni siquiera hace el pedido.
    }
}
