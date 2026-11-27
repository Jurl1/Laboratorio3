using System.Net;
using System.Net.Http.Json;
using Lab3.Tests.Infrastructure;
using Lab3.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lab3.Tests;

public class CatalogTests
{
    [Fact]
    public async Task Home_muestra_listas_y_atribucion_TMDB()
    {
        using var app = new LabFactory();
        var html = await app.Client().GetStringAsync("/");

        Assert.Contains("Películas en tendencia", html);
        Assert.Contains("Matrix", html);
        Assert.Contains("Breaking Bad", html);
        Assert.Contains("Ataque a los titanes", html);
        Assert.Contains("This product uses the TMDB API but is not endorsed or certified by TMDB", html);
        Assert.Contains("Santiago Juri Nam", html);
    }

    [Fact]
    public async Task Home_cachea_las_listas_en_la_base()
    {
        using var app = new LabFactory();
        var client = app.Client();
        await client.GetStringAsync("/");

        // Con TMDB caído, el home sigue saliendo del cache (6 h).
        app.Tmdb.Fail = true;
        var html = await client.GetStringAsync("/");
        Assert.Contains("Matrix", html);
        Assert.True(app.WithDb(db => db.ApiCache.Count()) >= 3);
    }

    [Fact]
    public async Task Home_sin_TMDB_muestra_aviso_y_no_se_cae()
    {
        using var app = new LabFactory();
        app.Tmdb.Fail = true;
        var res = await app.Client().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("El catálogo no está disponible", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Detalle_muestra_trailer_imdb_dondever_y_crea_el_titulo()
    {
        using var app = new LabFactory();
        var html = await app.Client().GetStringAsync("/titulo/pelicula/603");

        Assert.Contains("Matrix", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/trailerES", html);
        Assert.Contains("https://www.imdb.com/title/tt0133093/", html);
        Assert.Contains("JustWatch", html);
        Assert.Contains("Keanu Reeves", html);

        var title = app.WithDb(db => db.Titles.Single());
        Assert.Equal(603, title.TmdbId);
        Assert.Equal("tt0133093", title.ImdbId);
        Assert.Equal("trailerES", title.TrailerYoutubeKey);
        Assert.False(title.IsAnime);
    }

    [Fact]
    public async Task Detalle_de_anime_marca_IsAnime()
    {
        using var app = new LabFactory();
        var html = await app.Client().GetStringAsync("/titulo/serie/1429");
        Assert.Contains("Anime", html);
        Assert.True(app.WithDb(db => db.Titles.Single().IsAnime));
    }

    [Theory]
    [InlineData("/titulo/pelicula/999999")]
    [InlineData("/titulo/documental/603")]
    [InlineData("/no-existe")]
    public async Task Paginas_inexistentes_dan_404_amigable(string url)
    {
        using var app = new LabFactory();
        var res = await app.Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Contains("No encontramos esa página", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Busqueda_separa_peliculas_series_y_anime_y_descarta_personas()
    {
        using var app = new LabFactory();
        var client = app.Client();

        var movies = await client.GetStringAsync("/buscar?q=matrix&tab=peliculas");
        Assert.Contains("Matrix", movies);
        Assert.DoesNotContain("Breaking Bad</div>", movies);

        var series = await client.GetStringAsync("/buscar?q=matrix&tab=series");
        Assert.Contains("Breaking Bad", series);
        Assert.DoesNotContain("Ataque a los titanes</div>", series);

        var anime = await client.GetStringAsync("/buscar?q=matrix&tab=anime");
        Assert.Contains("Ataque a los titanes", anime);
        Assert.DoesNotContain("Keanu Reeves", anime);
    }

    [Fact]
    public async Task Videos_de_YouTube_se_cachean_por_titulo()
    {
        using var app = new LabFactory();
        var client = app.Client();
        await client.GetStringAsync("/titulo/pelicula/603");

        var first = await client.GetStringAsync("/titulo/pelicula/603/videos");
        var second = await client.GetStringAsync("/titulo/pelicula/603/videos");

        Assert.Equal(1, app.YouTube.Calls);
        Assert.Contains("abcDEF12345", first);
        // El título del video se escapa (no se inyecta HTML).
        Assert.Contains("análisis &lt;completo&gt;", first);
        Assert.Equal(first, second);
    }
}

public class AccountTests
{
    [Fact]
    public async Task Registro_guarda_hash_y_queda_logueado()
    {
        using var app = new LabFactory();
        var client = app.Client();
        await client.RegisterAsync("santi");

        var user = app.WithDb(db => db.Users.Single());
        Assert.Equal("santi", user.UserName);
        Assert.NotNull(user.PasswordHash);
        Assert.DoesNotContain("clave-segura-123", user.PasswordHash);

        var html = await client.GetStringAsync("/");
        Assert.Contains("/usuario/santi", html);
        Assert.Contains("Salir", html);
    }

    [Fact]
    public async Task Registro_rechaza_contrasenas_distintas_y_usuario_duplicado()
    {
        using var app = new LabFactory();
        var client = app.Client();

        var res = await client.PostFormAsync("/cuenta/registro", "/cuenta/registro", new()
        {
            ["UserName"] = "ana", ["DisplayName"] = "Ana", ["Email"] = "ana@test.local",
            ["Password"] = "clave-segura-123", ["ConfirmPassword"] = "otra-clave-123",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("Las contraseñas no coinciden", await res.Content.ReadAsStringAsync());

        await client.RegisterAsync("ana");
        var other = app.Client();
        res = await other.PostFormAsync("/cuenta/registro", "/cuenta/registro", new()
        {
            ["UserName"] = "ana", ["DisplayName"] = "Otra", ["Email"] = "otra@test.local",
            ["Password"] = "clave-segura-123", ["ConfirmPassword"] = "clave-segura-123",
        });
        Assert.Contains("ya está en uso", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_con_usuario_o_mail_logout_y_lockout()
    {
        using var app = new LabFactory();
        await app.Client().RegisterAsync("juan");

        var client = app.Client();
        var res = await client.PostFormAsync("/cuenta/login", "/cuenta/login", new() { ["Login"] = "juan@test.local", ["Password"] = "clave-segura-123" });
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("Salir", await client.GetStringAsync("/"));

        res = await client.PostFormAsync("/", "/cuenta/logout", []);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.DoesNotContain("Salir", await client.GetStringAsync("/"));

        // 5 intentos fallidos bloquean la cuenta (el quinto ya avisa del bloqueo).
        for (var i = 1; i <= 5; i++)
        {
            res = await client.PostFormAsync("/cuenta/login", "/cuenta/login", new() { ["Login"] = "juan", ["Password"] = "mal-mal-mal" });
            Assert.Contains(i < 5 ? "incorrectos" : "Demasiados intentos", await res.Content.ReadAsStringAsync());
        }
        // Bloqueada: ni con la contraseña correcta entra.
        res = await client.PostFormAsync("/cuenta/login", "/cuenta/login", new() { ["Login"] = "juan", ["Password"] = "clave-segura-123" });
        Assert.Contains("Demasiados intentos", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_sin_antiforgery_es_rechazado()
    {
        using var app = new LabFactory();
        var res = await app.Client().PostAsync("/cuenta/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Login"] = "x", ["Password"] = "y",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Editar_perfil_y_paginas_privadas_piden_login()
    {
        using var app = new LabFactory();
        var anon = app.Client();
        var res = await anon.GetAsync("/mis-listas");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/cuenta/login", res.Headers.Location!.PathAndQuery);

        var client = app.Client();
        await client.RegisterAsync("lucia");
        res = await client.PostFormAsync("/cuenta/perfil", "/cuenta/perfil", new() { ["DisplayName"] = "Lucía", ["Bio"] = "Fan del cine." });
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        var profile = await client.GetStringAsync("/usuario/lucia");
        Assert.Contains("Lucía", profile);
        Assert.Contains("Fan del cine.", profile);
    }

    [Fact]
    public async Task Pedido_HTTP_directo_no_rompe_las_paginas()
    {
        // Ej: un curl desde el LXC al contenedor, sin pasar por Cloudflare.
        using var app = new LabFactory();
        var res = await app.CreateClient().GetAsync("http://localhost/cuenta/login");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Detras_del_tunnel_las_cookies_son_Secure()
    {
        using var app = new LabFactory();
        var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/cuenta/login");
        req.Headers.Add("X-Forwarded-Proto", "https");
        var res = await app.CreateClient().SendAsync(req);
        // Solo las que guardan algo (las que borran una cookie vacía no importan).
        var cookies = res.Headers.GetValues("Set-Cookie").Where(c => !c.Contains("=;")).ToList();
        Assert.NotEmpty(cookies);
        Assert.All(cookies, c => Assert.Contains("secure", c, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Headers_de_seguridad_y_CSP()
    {
        using var app = new LabFactory();
        var res = await app.Client().GetAsync("/");
        Assert.Contains("frame-src https://www.youtube-nocookie.com", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
    }
}

public class ReviewAndListTests
{
    private static Dictionary<string, string> Review(int rating, string body, bool spoilers = false) => new()
    {
        ["Rating"] = rating.ToString(),
        ["Body"] = body,
        ["HasSpoilers"] = spoilers ? "true" : "false",
    };

    [Fact]
    public async Task Review_se_crea_edita_promedia_y_escapa_html()
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");
        var b = app.Client();
        await b.RegisterAsync("beto");

        var res = await a.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review", Review(9, "Muy buena <script>alert(1)</script>"));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        await b.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review", Review(6, "Le sobra media hora."));

        var html = await a.GetStringAsync("/titulo/pelicula/603");
        Assert.Contains("7.5", html.Replace(',', '.'));
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);

        // Editar no crea otra review: una por usuario por título.
        await a.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review", Review(10, "La volví a ver, es perfecta."));
        Assert.Equal(2, app.WithDb(db => db.Reviews.Count()));
        Assert.Equal(10, app.WithDb(db => db.Reviews.Single(r => r.User.UserName == "ana").Rating));
    }

    [Fact]
    public async Task Review_invalida_no_se_guarda()
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");

        var res = await a.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review", Review(11, "corta"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("El puntaje va de 1 a 10", html);
        Assert.Contains("Entre 10 y 5000 caracteres", html);
        Assert.Equal(0, app.WithDb(db => db.Reviews.Count()));
    }

    [Theory]
    [InlineData(1000, "Se estrena el 31 de diciembre de 2099")]
    [InlineData(1001, "Sin fecha de estreno")]
    public async Task No_se_puede_resenar_lo_que_no_se_estreno(int tmdbId, string aviso)
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");
        var url = $"/titulo/pelicula/{tmdbId}";

        var detail = await a.GetStringAsync(url);
        Assert.Contains(aviso, detail);
        Assert.DoesNotContain("Escribir review", detail);

        // Ni el formulario ni el POST directo.
        var res = await a.GetAsync(url + "/review");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal(url, res.Headers.Location!.OriginalString);
        Assert.Contains("Vas a poder reseñarlo cuando salga", await a.GetStringAsync(url));

        res = await a.PostFormAsync("/", url + "/review", Review(8, "Ya la vi en un sueño, buenísima."));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal(0, app.WithDb(db => db.Reviews.Count()));

        // Guardarla en "Pendiente" sí se puede.
        var token = await a.GetTokenAsync(url);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookmarks") { Content = JsonContent.Create(new { tipo = "pelicula", tmdbId, status = "Pendiente" }) };
        req.Headers.Add("RequestVerificationToken", token);
        Assert.Equal(HttpStatusCode.OK, (await a.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Pelicula_estrenada_guarda_fecha_y_se_puede_resenar()
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");
        Assert.Contains("Escribir review", await a.GetStringAsync("/titulo/pelicula/603"));
        Assert.Equal(new DateOnly(1999, 3, 30), app.WithDb(db => db.Titles.Single().ReleaseDate));
    }

    [Fact]
    public async Task Anonimo_no_puede_resenar()
    {
        using var app = new LabFactory();
        var res = await app.Client().GetAsync("/titulo/pelicula/603/review");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/cuenta/login", res.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Spoilers_ocultos_y_borrar_review()
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");
        await a.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review", Review(8, "Neo es el elegido al final.", spoilers: true));

        var html = await app.Client().GetStringAsync("/titulo/pelicula/603");
        Assert.Contains("Contiene spoilers", html);
        Assert.Contains("d-none\" data-spoiler-body", html);

        var res = await a.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review/borrar", []);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal(0, app.WithDb(db => db.Reviews.Count()));
    }

    [Fact]
    public async Task Like_por_fetch_con_header_y_no_a_la_propia()
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");
        await a.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review", Review(9, "Una obra maestra del cine."));
        var reviewId = app.WithDb(db => db.Reviews.Single().Id);

        var b = app.Client();
        await b.RegisterAsync("beto");
        var token = await b.GetTokenAsync("/titulo/pelicula/603");

        // Sin token: rechazado.
        var res = await b.PostAsync($"/api/reviews/{reviewId}/like", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/reviews/{reviewId}/like");
        req.Headers.Add("RequestVerificationToken", token);
        res = await b.SendAsync(req);
        var body = await res.Content.ReadFromJsonAsync<LikeResponse>();
        Assert.True(body!.Liked);
        Assert.Equal(1, body.Count);

        // Toggle: segundo click saca el like.
        req = new HttpRequestMessage(HttpMethod.Post, $"/api/reviews/{reviewId}/like");
        req.Headers.Add("RequestVerificationToken", token);
        body = await (await b.SendAsync(req)).Content.ReadFromJsonAsync<LikeResponse>();
        Assert.False(body!.Liked);
        Assert.Equal(0, body.Count);

        // La autora no puede darse like.
        var tokenA = await a.GetTokenAsync("/titulo/pelicula/603");
        req = new HttpRequestMessage(HttpMethod.Post, $"/api/reviews/{reviewId}/like");
        req.Headers.Add("RequestVerificationToken", tokenA);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Bookmarks_por_fetch_mis_listas_y_perfil_publico()
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");
        var token = await a.GetTokenAsync("/");

        async Task<HttpStatusCode> Save(string tipo, int id, string? status)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookmarks")
            {
                Content = JsonContent.Create(new { tipo, tmdbId = id, status }),
            };
            req.Headers.Add("RequestVerificationToken", token);
            return (await a.SendAsync(req)).StatusCode;
        }

        // El título se crea en la base al guardarlo, aunque nadie lo haya abierto.
        Assert.Equal(HttpStatusCode.OK, await Save("serie", 1429, "Viendo"));
        Assert.Equal(HttpStatusCode.OK, await Save("pelicula", 603, "Pendiente"));
        Assert.Equal(HttpStatusCode.OK, await Save("pelicula", 603, "Visto"));
        Assert.Equal(HttpStatusCode.NotFound, await Save("pelicula", 999999, "Visto"));
        Assert.Equal(HttpStatusCode.BadRequest, await Save("documental", 603, "Visto"));

        var lists = await a.GetStringAsync("/mis-listas?estado=visto");
        Assert.Contains("Matrix", lists);
        Assert.DoesNotContain("Ataque a los titanes", lists);

        var profile = await app.Client().GetStringAsync("/usuario/ana");
        Assert.Contains("Ataque a los titanes", profile);
        Assert.Contains("Viendo", profile);

        // status null = quitar de las listas.
        Assert.Equal(HttpStatusCode.OK, await Save("pelicula", 603, null));
        Assert.Equal(1, app.WithDb(db => db.Bookmarks.Count()));
    }

    [Fact]
    public async Task Borrar_usuario_borra_sus_reviews_likes_y_bookmarks()
    {
        using var app = new LabFactory();
        var a = app.Client();
        await a.RegisterAsync("ana");
        await a.PostFormAsync("/titulo/pelicula/603/review", "/titulo/pelicula/603/review", Review(9, "Una obra maestra del cine."));

        app.WithDb(db =>
        {
            var user = db.Users.Single();
            var title = db.Titles.Single();
            db.Bookmarks.Add(new Bookmark { UserId = user.Id, TitleId = title.Id, Status = BookmarkStatus.Visto });
            db.SaveChanges();
            db.Users.Remove(user);
            return db.SaveChanges();
        });

        Assert.Equal(0, app.WithDb(db => db.Reviews.Count() + db.Bookmarks.Count()));
    }

    [Fact]
    public async Task Datos_demo_se_cargan_una_sola_vez_y_aparecen_en_el_home()
    {
        using var app = new LabFactory();
        async Task Seed()
        {
            using var scope = app.Services.CreateScope();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Password"] = "demo-clave-123" })
                .Build();
            await ActivatorUtilities.CreateInstance<DemoSeeder>(scope.ServiceProvider, (IConfiguration)config).SeedAsync();
        }

        await Seed();
        await Seed();

        Assert.Equal(3, app.WithDb(db => db.Users.Count()));
        Assert.Equal(7, app.WithDb(db => db.Reviews.Count()));
        // Los títulos que TMDB conoce quedan sincronizados (póster, IMDb).
        Assert.Equal("tt0133093", app.WithDb(db => db.Titles.Single(t => t.TmdbId == 603).ImdbId));

        var html = await app.Client().GetStringAsync("/");
        Assert.Contains("Últimas reviews de la comunidad", html);
        Assert.Contains("@cinefila", html);
    }

    private sealed record LikeResponse(bool Liked, int Count);
}
