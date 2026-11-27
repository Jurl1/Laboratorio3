using Lab3.Web.Data;
using Lab3.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Services;

/// <summary>
/// Puntajes del usuario logueado, para mostrarlos en cada póster.
/// Se cargan una sola vez por request (una consulta), no una por tarjeta.
/// </summary>
public sealed class MyRatings(AppDbContext db, IHttpContextAccessor http)
{
    private Dictionary<(TmdbType, int), int>? _ratings;

    public async Task<int?> GetAsync(TmdbType type, int tmdbId)
    {
        if (_ratings is null)
        {
            var me = http.HttpContext?.User.UserId();
            _ratings = me is null
                ? []
                : (await db.Reviews
                    .Where(r => r.UserId == me)
                    .Select(r => new { r.Title.TmdbType, r.Title.TmdbId, r.Rating })
                    .ToListAsync())
                    .ToDictionary(x => (x.TmdbType, x.TmdbId), x => x.Rating);
        }

        return _ratings.TryGetValue((type, tmdbId), out var rating) ? rating : null;
    }
}
