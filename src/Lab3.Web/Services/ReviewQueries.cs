using Lab3.Web.Data;
using Lab3.Web.Models;

namespace Lab3.Web.Services;

public static class ReviewQueries
{
    /// <summary>Proyección a ReviewItem traducible a SQL (cuenta likes y si el usuario actual dio like).</summary>
    public static IQueryable<ReviewItem> ToItems(this IQueryable<Review> query, string? currentUserId) =>
        query.Select(r => new ReviewItem(
            r.Id,
            r.User.UserName!,
            r.User.DisplayName,
            r.Rating,
            r.Body,
            r.HasSpoilers,
            r.CreatedAt,
            r.UpdatedAt,
            r.Likes.Count,
            currentUserId != null && r.Likes.Any(l => l.UserId == currentUserId),
            currentUserId != null && r.UserId == currentUserId,
            r.Title.TmdbType,
            r.Title.TmdbId,
            r.Title.Name,
            r.Title.PosterPath));

    public static IQueryable<BookmarkItem> ToItems(this IQueryable<Bookmark> query) =>
        query.Select(b => new BookmarkItem(
            b.Title.TmdbType,
            b.Title.TmdbId,
            b.Title.Name,
            b.Title.Year,
            b.Title.PosterPath,
            b.Title.IsAnime,
            b.Status,
            b.CreatedAt));
}
