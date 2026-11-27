using Lab3.Web.Data;
using Lab3.Web.Infrastructure;
using Lab3.Web.Models;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Controllers;

/// <summary>
/// Endpoints que usa el JS con fetch (likes y bookmarks) sin recargar la página.
/// El anti-forgery token viaja en el header RequestVerificationToken (filtro global).
/// </summary>
[Authorize]
[Route("api")]
public class ApiController(AppDbContext db, ICatalogService catalog) : Controller
{
    [HttpPost("reviews/{id:int}/like")]
    public async Task<IActionResult> ToggleLike(int id, CancellationToken ct)
    {
        var me = User.UserId()!;
        var review = await db.Reviews.Where(r => r.Id == id).Select(r => new { r.Id, r.UserId }).FirstOrDefaultAsync(ct);
        if (review is null) return NotFound();
        if (review.UserId == me) return BadRequest(new { error = "No podés darle like a tu propia review." });

        var like = await db.ReviewLikes.FindAsync([me, id], ct);
        if (like is null)
            db.ReviewLikes.Add(new ReviewLike { UserId = me, ReviewId = id });
        else
            db.ReviewLikes.Remove(like);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Doble click: el like ya existía.
            db.ChangeTracker.Clear();
        }

        var count = await db.ReviewLikes.CountAsync(l => l.ReviewId == id, ct);
        var liked = await db.ReviewLikes.AnyAsync(l => l.ReviewId == id && l.UserId == me, ct);
        return Json(new { liked, count });
    }

    /// <summary>Guarda o cambia el estado. Con status null, saca el título de las listas.</summary>
    [HttpPost("bookmarks")]
    public async Task<IActionResult> SetBookmark([FromBody] BookmarkRequest req, CancellationToken ct)
    {
        if (Display.ParseSlug(req.Tipo) is not { } type) return BadRequest(new { error = "Tipo inválido." });
        if (req.Status is { } s && !Enum.IsDefined(s)) return BadRequest(new { error = "Estado inválido." });

        Title? title;
        try
        {
            title = await catalog.EnsureTitleAsync(type, req.TmdbId, ct);
        }
        catch (CatalogUnavailableException)
        {
            title = await db.Titles.FirstOrDefaultAsync(t => t.TmdbType == type && t.TmdbId == req.TmdbId, ct);
        }
        if (title is null) return NotFound();

        var me = User.UserId()!;
        var bookmark = await db.Bookmarks.FindAsync([me, title.Id], ct);

        if (req.Status is null)
        {
            if (bookmark is not null) db.Bookmarks.Remove(bookmark);
        }
        else if (bookmark is null)
        {
            db.Bookmarks.Add(new Bookmark { UserId = me, TitleId = title.Id, Status = req.Status.Value, CreatedAt = DateTime.UtcNow });
        }
        else
        {
            bookmark.Status = req.Status.Value;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Conflict(new { error = "Probá de nuevo." });
        }

        return Json(new { status = req.Status?.ToString() });
    }
}
