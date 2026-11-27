using Microsoft.AspNetCore.Identity;

namespace Lab3.Web.Data;

// Modelo de datos de PLAN.md §5.

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Review> Reviews { get; set; } = [];
    public List<Bookmark> Bookmarks { get; set; } = [];
}

/// <summary>Endpoint de TMDB al que pertenece el título.</summary>
public enum TmdbType
{
    Movie = 0,
    Tv = 1,
}

/// <summary>Cache local de un título de TMDB.</summary>
public class Title
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public TmdbType TmdbType { get; set; }
    public bool IsAnime { get; set; }
    public string? ImdbId { get; set; }
    public string Name { get; set; } = "";
    public string? OriginalName { get; set; }
    public int? Year { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string GenresJson { get; set; } = "[]";
    public string? TrailerYoutubeKey { get; set; }
    public DateTime LastSyncedAt { get; set; }

    public List<Review> Reviews { get; set; } = [];
    public List<Bookmark> Bookmarks { get; set; } = [];
}

public class Review
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;

    /// <summary>Puntaje de 1 a 10.</summary>
    public int Rating { get; set; }
    public string Body { get; set; } = "";
    public bool HasSpoilers { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ReviewLike> Likes { get; set; } = [];
}

public class ReviewLike
{
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int ReviewId { get; set; }
    public Review Review { get; set; } = null!;
}

public enum BookmarkStatus
{
    Pendiente = 0,
    Viendo = 1,
    Visto = 2,
    Abandonado = 3,
}

public class Bookmark
{
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public BookmarkStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Respuestas de APIs externas cacheadas (TMDB, YouTube).</summary>
public class ApiCacheEntry
{
    public string Key { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
