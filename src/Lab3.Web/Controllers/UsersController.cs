using Lab3.Web.Data;
using Lab3.Web.Infrastructure;
using Lab3.Web.Models;
using Lab3.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Controllers;

public class UsersController(AppDbContext db) : Controller
{
    /// <summary>Perfil público: reviews y bookmarks (PLAN.md §7).</summary>
    [HttpGet("/usuario/{username}")]
    public async Task<IActionResult> Profile(string username, CancellationToken ct)
    {
        var normalized = username.ToUpperInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
        if (user is null) return NotFound();

        var me = User.UserId();
        return View(new UserProfileViewModel
        {
            UserName = user.UserName!,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            CreatedAt = user.CreatedAt,
            IsMe = me == user.Id,
            Reviews = await db.Reviews
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToItems(me)
                .ToListAsync(ct),
            Bookmarks = await db.Bookmarks
                .Where(b => b.UserId == user.Id)
                .OrderBy(b => b.Status)
                .ThenByDescending(b => b.CreatedAt)
                .ToItems()
                .ToListAsync(ct),
        });
    }

    /// <summary>Bookmarks del usuario logueado, filtrados por estado.</summary>
    [Authorize]
    [HttpGet("/mis-listas")]
    public async Task<IActionResult> MyLists(string? estado, CancellationToken ct)
    {
        var me = User.UserId()!;
        var all = db.Bookmarks.Where(b => b.UserId == me);

        var counts = await all.GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        BookmarkStatus? filter = Enum.TryParse<BookmarkStatus>(estado, ignoreCase: true, out var s) && Enum.IsDefined(s) ? s : null;
        var query = filter is null ? all : all.Where(b => b.Status == filter);

        return View(new MyListsViewModel
        {
            Status = filter?.ToString().ToLowerInvariant() ?? "todos",
            Counts = counts,
            Items = await query.OrderByDescending(b => b.CreatedAt).ToItems().ToListAsync(ct),
        });
    }
}
