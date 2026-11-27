using System.Text.Json;
using Lab3.Web.Data;
using Lab3.Web.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lab3.Web.Services.Catalog;

public interface ICatalogService
{
    Task<HomeLists> GetHomeAsync(CancellationToken ct = default);
    Task<SearchResults> SearchAsync(string query, int page, CancellationToken ct = default);
    /// <summary>Null si el título no existe en TMDB.</summary>
    Task<TitleDetail?> GetDetailAsync(TmdbType type, int tmdbId, CancellationToken ct = default);
    /// <summary>Crea o actualiza la fila local del título (PLAN.md §5: se crea al abrirlo, reseñarlo o guardarlo).</summary>
    Task<Title?> EnsureTitleAsync(TmdbType type, int tmdbId, CancellationToken ct = default);
    Task<Title> SyncTitleAsync(TitleDetail detail, CancellationToken ct = default);
}

/// <summary>Normaliza TMDB a DTOs, marca IsAnime y maneja el cache. Los controllers solo hablan con este servicio.</summary>
public sealed class CatalogService(
    ITmdbClient tmdb,
    ApiCache cache,
    AppDbContext db,
    IOptions<TmdbOptions> options,
    TimeProvider clock) : ICatalogService
{
    // PLAN.md §6: listas del home cada 6 h, detalle cada 24 h.
    private static readonly TimeSpan HomeTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan DetailTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SearchTtl = TimeSpan.FromHours(1);
    private const int CardsPerRow = 18;

    private readonly TmdbOptions _opt = options.Value;

    public async Task<HomeLists> GetHomeAsync(CancellationToken ct = default)
    {
        var lang = _opt.Language;
        var movies = await cache.GetOrCreateAsync($"tmdb:trending:movie:week:{lang}", HomeTtl,
            async c => Cards(await tmdb.GetTrendingAsync(TmdbType.Movie, c), TmdbType.Movie), ct);
        var tv = await cache.GetOrCreateAsync($"tmdb:trending:tv:week:{lang}", HomeTtl,
            async c => Cards(await tmdb.GetTrendingAsync(TmdbType.Tv, c), TmdbType.Tv), ct);
        var anime = await cache.GetOrCreateAsync($"tmdb:anime:tv:popular:{lang}", HomeTtl,
            async c => Cards(await tmdb.DiscoverAnimeAsync(TmdbType.Tv, c), TmdbType.Tv, forceAnime: true), ct);

        return new HomeLists(movies, tv, anime);
    }

    public async Task<SearchResults> SearchAsync(string query, int page, CancellationToken ct = default)
    {
        query = query.Trim();
        page = Math.Clamp(page, 1, 500);
        if (query.Length == 0) return new SearchResults([], 1, 0);

        var key = $"tmdb:search:{_opt.Language}:{page}:{ApiCache.Hash(query.ToLowerInvariant())}";
        return await cache.GetOrCreateAsync(key, SearchTtl, async c =>
        {
            var res = await tmdb.SearchMultiAsync(query, page, c);
            var items = res.Results
                .Select(r => (item: r, type: TmdbMapper.TypeOf(r)))
                .Where(x => x.type is not null)
                .Select(x => TmdbMapper.ToCard(x.item, x.type!.Value))
                .ToList();
            return new SearchResults(items, Math.Max(res.Page, 1), Math.Min(res.TotalPages, 500));
        }, ct);
    }

    public async Task<TitleDetail?> GetDetailAsync(TmdbType type, int tmdbId, CancellationToken ct = default)
    {
        var key = $"tmdb:{TmdbClient.Segment(type)}:{tmdbId}:{_opt.Language}:{_opt.WatchRegion}";
        // Un 404 también se cachea (como null) para no repetir el pedido a TMDB.
        var holder = await cache.GetOrCreateAsync(key, DetailTtl, async c =>
        {
            var item = await tmdb.GetDetailAsync(type, tmdbId, c);
            return new DetailHolder(item is null ? null : TmdbMapper.ToDetail(item, type, _opt.WatchRegion));
        }, ct);
        return holder.Detail;
    }

    public async Task<Title?> EnsureTitleAsync(TmdbType type, int tmdbId, CancellationToken ct = default)
    {
        var existing = await db.Titles.FirstOrDefaultAsync(t => t.TmdbType == type && t.TmdbId == tmdbId, ct);
        if (existing is not null && existing.LastSyncedAt > Now - DetailTtl)
            return existing;

        var detail = await GetDetailAsync(type, tmdbId, ct);
        if (detail is null) return existing;
        return await SyncTitleAsync(detail, ct);
    }

    public async Task<Title> SyncTitleAsync(TitleDetail d, CancellationToken ct = default)
    {
        var title = await db.Titles.FirstOrDefaultAsync(t => t.TmdbType == d.Type && t.TmdbId == d.TmdbId, ct);
        if (title is not null && title.LastSyncedAt > Now - DetailTtl)
            return title;

        if (title is null)
        {
            title = new Title { TmdbType = d.Type, TmdbId = d.TmdbId };
            db.Titles.Add(title);
        }

        title.Name = Truncate(d.Name, 300);
        title.OriginalName = d.OriginalName is null ? null : Truncate(d.OriginalName, 300);
        title.Year = d.Year;
        title.Overview = d.Overview;
        title.PosterPath = d.PosterPath;
        title.BackdropPath = d.BackdropPath;
        title.GenresJson = JsonSerializer.Serialize(d.Genres);
        title.ImdbId = d.ImdbId;
        title.TrailerYoutubeKey = d.TrailerKey;
        title.IsAnime = d.IsAnime;
        title.LastSyncedAt = Now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Otro request creó el mismo título en paralelo (índice único): usar ese.
            db.ChangeTracker.Clear();
            title = await db.Titles.FirstAsync(t => t.TmdbType == d.Type && t.TmdbId == d.TmdbId, ct);
        }

        return title;
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static List<TitleCard> Cards(TmdbPage page, TmdbType type, bool? forceAnime = null) =>
        page.Results.Take(CardsPerRow).Select(r => TmdbMapper.ToCard(r, type, forceAnime)).ToList();

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed record DetailHolder(TitleDetail? Detail);
}
