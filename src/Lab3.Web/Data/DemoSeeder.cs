using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Data;

/// <summary>
/// Datos de demo para la entrega (PLAN.md §9, fase 8). Se activa con Demo__Seed=true.
/// La contraseña de los usuarios demo sale de Demo__Password (nunca en git).
/// Es idempotente: si los usuarios ya existen, no hace nada.
/// </summary>
public sealed class DemoSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    ICatalogService catalog,
    IConfiguration config,
    ILogger<DemoSeeder> logger)
{
    private static readonly (string User, string Name, string Bio)[] DemoUsers =
    [
        ("cinefila", "Martina", "Veo todo lo que dura más de tres horas."),
        ("seriero", "Lucas", "Maratones de series los domingos."),
        ("otaku_ar", "Sofía", "Anime de temporada y clásicos de Ghibli."),
    ];

    // Títulos conocidos de TMDB. El resto de los datos (póster, sinopsis) se completa
    // solo la primera vez que alguien abre la página: LastSyncedAt viejo fuerza el sync.
    private static readonly (TmdbType Type, int TmdbId, string Name, int Year, bool Anime)[] DemoTitles =
    [
        (TmdbType.Movie, 603, "Matrix", 1999, false),
        (TmdbType.Movie, 129, "El viaje de Chihiro", 2001, true),
        (TmdbType.Tv, 1396, "Breaking Bad", 2008, false),
        (TmdbType.Tv, 1429, "Attack on Titan", 2013, true),
        (TmdbType.Movie, 27205, "El origen", 2010, false),
    ];

    private static readonly (int User, int Title, int Rating, string Body, bool Spoilers)[] DemoReviews =
    [
        (0, 0, 10, "Cambió el cine de acción para siempre. Los efectos siguen aguantando más de veinte años después.", false),
        (0, 4, 9, "Un rompecabezas que pide verla dos veces. La música de Zimmer hace la mitad del trabajo.", false),
        (0, 1, 9, "Preciosa de punta a punta. Cada fondo es una pintura.", false),
        (1, 2, 10, "La mejor serie que vi. La transformación del protagonista está construida con una paciencia enorme.", false),
        (1, 3, 8, "Arranca muy fuerte y el final divide opiniones. El giro de la temporada 3 sobre el muro cambia todo.", true),
        (2, 3, 9, "La animación de las peleas es espectacular. Ideal para engancharse con el anime.", false),
        (2, 1, 10, "Mi película favorita de Ghibli. La recomiendo siempre como primera película de anime.", false),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var password = config["Demo:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            logger.LogWarning("Demo__Seed está activo pero falta Demo__Password (mínimo 8 caracteres). No se cargan datos demo.");
            return;
        }

        if (await users.FindByNameAsync(DemoUsers[0].User) is not null)
        {
            logger.LogInformation("Los datos demo ya estaban cargados.");
            return;
        }

        var created = new List<ApplicationUser>();
        foreach (var (userName, name, bio) in DemoUsers)
        {
            var user = new ApplicationUser
            {
                UserName = userName,
                Email = $"{userName}@demo.lab3.local",
                EmailConfirmed = true,
                DisplayName = name,
                Bio = bio,
                CreatedAt = DateTime.UtcNow,
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogWarning("No se pudo crear {User}: {Errors}", userName, string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            created.Add(user);
        }

        var titles = new List<Title>();
        foreach (var (type, tmdbId, name, year, anime) in DemoTitles)
        {
            var title = await db.Titles.FirstOrDefaultAsync(t => t.TmdbType == type && t.TmdbId == tmdbId, ct);
            if (title is null)
            {
                title = new Title
                {
                    TmdbType = type,
                    TmdbId = tmdbId,
                    Name = name,
                    Year = year,
                    IsAnime = anime,
                    LastSyncedAt = DateTime.UnixEpoch, // Viejo a propósito: fuerza el sync con TMDB.
                };
                db.Titles.Add(title);
            }
            titles.Add(title);
        }
        await db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;
        var i = 0;
        foreach (var (u, t, rating, body, spoilers) in DemoReviews)
        {
            var when = now.AddHours(-6 * ++i);
            db.Reviews.Add(new Review
            {
                UserId = created[u].Id,
                TitleId = titles[t].Id,
                Rating = rating,
                Body = body,
                HasSpoilers = spoilers,
                CreatedAt = when,
                UpdatedAt = when,
            });
        }
        await db.SaveChangesAsync(ct);

        // Algunos likes y bookmarks para que se vea vivo.
        var reviews = await db.Reviews.Where(r => created.Select(c => c.Id).Contains(r.UserId)).ToListAsync(ct);
        foreach (var r in reviews)
            foreach (var u in created.Where(c => c.Id != r.UserId).Take(r.Rating >= 10 ? 2 : 1))
                db.ReviewLikes.Add(new ReviewLike { UserId = u.Id, ReviewId = r.Id });

        db.Bookmarks.AddRange(
            new Bookmark { UserId = created[0].Id, TitleId = titles[2].Id, Status = BookmarkStatus.Pendiente, CreatedAt = now },
            new Bookmark { UserId = created[1].Id, TitleId = titles[3].Id, Status = BookmarkStatus.Viendo, CreatedAt = now },
            new Bookmark { UserId = created[2].Id, TitleId = titles[0].Id, Status = BookmarkStatus.Visto, CreatedAt = now });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Datos demo cargados: {Users} usuarios, {Reviews} reviews.", created.Count, DemoReviews.Length);

        // Completar pósters y sinopsis desde TMDB si está disponible (si no, se completa al abrir cada título).
        foreach (var (type, tmdbId, _, _, _) in DemoTitles)
        {
            try { await catalog.EnsureTitleAsync(type, tmdbId, ct); }
            catch (CatalogUnavailableException) { break; }
        }
    }
}
