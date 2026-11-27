using Lab3.Tests.Infrastructure;
using Lab3.Web.Data;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;

namespace Lab3.Tests;

public class TmdbMapperTests
{
    [Fact]
    public void Anime_con_genero_16_y_keyword_anime()
    {
        var item = new TmdbItem { Genres = [new() { Id = 16 }], OriginCountry = ["US"], Keywords = new() { Keywords = [new() { Id = 210024 }] } };
        Assert.True(TmdbMapper.IsAnimeDetail(item));
    }

    [Fact]
    public void Anime_con_genero_16_y_origen_JP_sin_keyword()
    {
        var item = new TmdbItem { Genres = [new() { Id = 16 }], OriginCountry = ["JP"] };
        Assert.True(TmdbMapper.IsAnimeDetail(item));
    }

    [Fact]
    public void Animacion_occidental_no_es_anime()
    {
        var item = new TmdbItem { Genres = [new() { Id = 16 }], OriginCountry = ["US"], OriginalLanguage = "en" };
        Assert.False(TmdbMapper.IsAnimeDetail(item));
    }

    [Fact]
    public void Japones_sin_genero_animacion_no_es_anime()
    {
        var item = new TmdbItem { Genres = [new() { Id = 18 }], OriginCountry = ["JP"], Keywords = new() { Results = [new() { Id = 210024 }] } };
        Assert.False(TmdbMapper.IsAnimeDetail(item));
    }

    [Fact]
    public void Trailer_prioriza_espanol_y_solo_YouTube_Trailer()
    {
        Assert.Equal("trailerES", TmdbMapper.PickTrailer(FakeTmdb.Matrix().Videos));
        Assert.Null(TmdbMapper.PickTrailer(new TmdbVideos { Results = [new() { Key = "x", Site = "Vimeo", Type = "Trailer" }] }));
    }

    [Theory]
    [InlineData("1999-03-30", 1999)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("abc", null)]
    public void Year_desde_fecha(string? date, int? expected) => Assert.Equal(expected, TmdbMapper.YearOf(date));

    [Fact]
    public void Detalle_toma_imdb_de_external_ids_y_dondever_de_AR()
    {
        var d = TmdbMapper.ToDetail(FakeTmdb.Matrix(), TmdbType.Movie, "AR");
        Assert.Equal("tt0133093", d.ImdbId);
        Assert.Equal("Matrix", d.Name);
        Assert.Equal(1999, d.Year);
        Assert.Equal(8.2, d.VoteAverage);
        Assert.NotNull(d.Providers);
        Assert.Equal("Max", Assert.Single(d.Providers!.Flatrate).Name);
        Assert.False(d.IsAnime);

        Assert.Null(TmdbMapper.ToDetail(FakeTmdb.Matrix(), TmdbType.Movie, "US").Providers);
    }
}
