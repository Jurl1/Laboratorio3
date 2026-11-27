using Lab3.Web.Data;
using Lab3.Web.Infrastructure;
using Lab3.Web.Models;
using Lab3.Web.Services;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;
using Lab3.Web.Services.YouTube;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Controllers;

[Route("titulo/{tipo}/{id:int}")]
public class TitlesController(
    ICatalogService catalog,
    AppDbContext db,
    IYouTubeClient youtube,
    ApiCache cache,
    TimeProvider clock,
    ILogger<TitlesController> logger) : Controller
{
    // ---------- Detalle ----------

    [HttpGet("")]
    public async Task<IActionResult> Details(string tipo, int id, CancellationToken ct)
    {
        if (Display.ParseSlug(tipo) is not { } type) return NotFound();

        TitleDetail? detail;
        try
        {
            detail = await catalog.GetDetailAsync(type, id, ct);
        }
        catch (CatalogUnavailableException ex)
        {
            ViewData["CatalogError"] = ex.Message;
            return View("CatalogUnavailable");
        }
        if (detail is null) return NotFound();

        var title = await catalog.SyncTitleAsync(detail, ct);
        var me = User.UserId();

        var reviews = await db.Reviews
            .Where(r => r.TitleId == title.Id)
            .OrderByDescending(r => r.Likes.Count)
            .ThenByDescending(r => r.CreatedAt)
            .ToItems(me)
            .ToListAsync(ct);

        // Puntaje de la comunidad: AVG(Rating) sobre Reviews (PLAN.md §5).
        double? average = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : null;

        BookmarkStatus? bookmark = null;
        if (me is not null)
        {
            bookmark = await db.Bookmarks
                .Where(b => b.UserId == me && b.TitleId == title.Id)
                .Select(b => (BookmarkStatus?)b.Status)
                .FirstOrDefaultAsync(ct);
        }

        return View(new TitleViewModel
        {
            Detail = detail,
            TitleId = title.Id,
            CommunityAverage = average,
            CommunityCount = reviews.Count,
            Reviews = reviews,
            MyReview = reviews.FirstOrDefault(r => r.IsMine),
            MyBookmark = bookmark,
            YouTubeEnabled = youtube.IsConfigured,
            IsReleased = Release.IsReleased(detail.ReleaseDate, Display.Today(clock)),
        });
    }

    // ---------- Review: crear / editar / borrar ----------

    [Authorize]
    [HttpGet("review")]
    public async Task<IActionResult> Review(string tipo, int id, CancellationToken ct)
    {
        if (Display.ParseSlug(tipo) is not { } type) return NotFound();
        var title = await EnsureTitle(type, id, ct);
        if (title is null) return NotFound();
        if (NotReleased(title) is { } blocked) return blocked;

        var me = User.UserId()!;
        var existing = await db.Reviews.FirstOrDefaultAsync(r => r.UserId == me && r.TitleId == title.Id, ct);

        var model = FormFor(title);
        if (existing is not null)
        {
            model.IsEdit = true;
            model.Rating = existing.Rating;
            model.Body = existing.Body;
            model.HasSpoilers = existing.HasSpoilers;
        }
        return View(model);
    }

    [Authorize]
    [HttpPost("review")]
    public async Task<IActionResult> Review(string tipo, int id, ReviewFormViewModel input, CancellationToken ct)
    {
        if (Display.ParseSlug(tipo) is not { } type) return NotFound();
        var title = await EnsureTitle(type, id, ct);
        if (title is null) return NotFound();
        if (NotReleased(title) is { } blocked) return blocked;

        var me = User.UserId()!;
        var existing = await db.Reviews.FirstOrDefaultAsync(r => r.UserId == me && r.TitleId == title.Id, ct);

        if (!ModelState.IsValid)
        {
            var model = FormFor(title);
            model.IsEdit = existing is not null;
            model.Rating = input.Rating;
            model.Body = input.Body;
            model.HasSpoilers = input.HasSpoilers;
            return View(model);
        }

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            // Una review por usuario por título (índice único).
            db.Reviews.Add(new Review
            {
                UserId = me,
                TitleId = title.Id,
                Rating = input.Rating!.Value,
                Body = input.Body.Trim(),
                HasSpoilers = input.HasSpoilers,
                CreatedAt = now,
                UpdatedAt = now,
            });
            TempData["Flash"] = "¡Review publicada!";
        }
        else
        {
            existing.Rating = input.Rating!.Value;
            existing.Body = input.Body.Trim();
            existing.HasSpoilers = input.HasSpoilers;
            existing.UpdatedAt = now;
            TempData["Flash"] = "Review actualizada.";
        }

        await db.SaveChangesAsync(ct);
        return Redirect(Display.TitleUrl(type, id) + "#reviews");
    }

    [Authorize]
    [HttpPost("review/borrar")]
    public async Task<IActionResult> DeleteReview(string tipo, int id, CancellationToken ct)
    {
        if (Display.ParseSlug(tipo) is not { } type) return NotFound();
        var me = User.UserId()!;

        var review = await db.Reviews
            .FirstOrDefaultAsync(r => r.UserId == me && r.Title.TmdbType == type && r.Title.TmdbId == id, ct);
        if (review is not null)
        {
            db.Reviews.Remove(review);
            await db.SaveChangesAsync(ct);
            TempData["Flash"] = "Review borrada.";
        }
        return Redirect(Display.TitleUrl(type, id) + "#reviews");
    }

    // ---------- Pestaña "Videos" (YouTube Data API) ----------

    /// <summary>
    /// Se pide con fetch solo cuando el usuario abre la pestaña. search.list cuesta ~100 unidades de
    /// las 10.000 diarias, así que se cachea 7 días por título (PLAN.md §6).
    /// </summary>
    [HttpGet("videos")]
    public async Task<IActionResult> Videos(string tipo, int id, CancellationToken ct)
    {
        if (Display.ParseSlug(tipo) is not { } type) return NotFound();
        var model = new VideosViewModel();

        if (!youtube.IsConfigured)
        {
            model.Error = "Los videos de YouTube no están configurados.";
            return PartialView("_Videos", model);
        }

        var title = await db.Titles.AsNoTracking().FirstOrDefaultAsync(t => t.TmdbType == type && t.TmdbId == id, ct);
        if (title is null) return NotFound();

        try
        {
            var query = $"{title.OriginalName ?? title.Name}{(title.Year is { } y ? $" {y}" : "")} review";
            model.Videos = await cache.GetOrCreateAsync(
                $"yt:search:{TmdbClient.Segment(type)}:{id}",
                TimeSpan.FromDays(7),
                c => youtube.SearchAsync(query, 6, c),
                ct);
        }
        catch (YouTubeUnavailableException ex)
        {
            logger.LogWarning("YouTube no disponible: {Message}", ex.Message);
            model.Error = "No pudimos cargar los videos ahora. Probá más tarde.";
        }

        return PartialView("_Videos", model);
    }

    private async Task<Title?> EnsureTitle(TmdbType type, int id, CancellationToken ct)
    {
        try
        {
            return await catalog.EnsureTitleAsync(type, id, ct);
        }
        catch (CatalogUnavailableException)
        {
            // Si TMDB no responde pero el título ya está en la base, se puede reseñar igual.
            return await db.Titles.FirstOrDefaultAsync(t => t.TmdbType == type && t.TmdbId == id, ct);
        }
    }

    /// <summary>No se reseñan títulos sin estrenar (sin fecha en TMDB o con fecha futura). Borrar sí se puede.</summary>
    private IActionResult? NotReleased(Title title)
    {
        if (title.IsReleased(Display.Today(clock))) return null;

        TempData["FlashError"] = title.ReleaseDate is { } date
            ? $"{title.Name} se estrena el {Display.LongDate(date)}. Vas a poder reseñarlo cuando salga."
            : $"{title.Name} todavía no tiene fecha de estreno. Vas a poder reseñarlo cuando salga.";
        return Redirect(Display.TitleUrl(title.TmdbType, title.TmdbId));
    }

    private static ReviewFormViewModel FormFor(Title title) => new()
    {
        Type = title.TmdbType,
        TmdbId = title.TmdbId,
        TitleName = title.Name,
        TitleYear = title.Year,
        TitlePoster = title.PosterPath,
    };
}
