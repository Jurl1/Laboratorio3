using System.ComponentModel.DataAnnotations;
using Lab3.Web.Data;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.YouTube;

namespace Lab3.Web.Models;

// ---------- Cuenta ----------

public class RegisterViewModel
{
    [Required(ErrorMessage = "Elegí un nombre de usuario.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Entre 3 y 30 caracteres.")]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Solo letras, números, punto, guion y guion bajo.")]
    [Display(Name = "Usuario")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Ingresá tu nombre.")]
    [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    [Display(Name = "Nombre para mostrar")]
    public string DisplayName { get; set; } = "";

    [Required(ErrorMessage = "Ingresá tu mail.")]
    [EmailAddress(ErrorMessage = "El mail no es válido.")]
    [StringLength(256)]
    [Display(Name = "Mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Ingresá una contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Repetí la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repetir contraseña")]
    public string ConfirmPassword { get; set; } = "";
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Ingresá tu usuario o mail.")]
    [Display(Name = "Usuario o mail")]
    public string Login { get; set; } = "";

    [Required(ErrorMessage = "Ingresá tu contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    [Display(Name = "Recordarme")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Ingresá tu nombre.")]
    [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    [Display(Name = "Nombre para mostrar")]
    public string DisplayName { get; set; } = "";

    [StringLength(500, ErrorMessage = "Máximo 500 caracteres.")]
    [Display(Name = "Sobre mí")]
    public string? Bio { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Ingresá tu contraseña actual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Ingresá la nueva contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Repetí la nueva contraseña.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repetir nueva contraseña")]
    public string ConfirmPassword { get; set; } = "";
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Ingresá tu mail.")]
    [EmailAddress(ErrorMessage = "El mail no es válido.")]
    [Display(Name = "Mail")]
    public string Email { get; set; } = "";
}

public class ResetPasswordViewModel
{
    [Required] public string UserId { get; set; } = "";
    [Required] public string Token { get; set; } = "";

    [Required(ErrorMessage = "Ingresá la nueva contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Repetí la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repetir contraseña")]
    public string ConfirmPassword { get; set; } = "";
}

// ---------- Catálogo ----------

/// <summary>Una review lista para mostrar.</summary>
public record ReviewItem(
    int Id,
    string UserName,
    string DisplayName,
    int Rating,
    string Body,
    bool HasSpoilers,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int LikeCount,
    bool LikedByMe,
    bool IsMine,
    // Datos del título (para el home y el perfil).
    TmdbType TitleType,
    int TitleTmdbId,
    string TitleName,
    string? TitlePoster);

public class HomeViewModel
{
    public HomeLists? Lists { get; set; }
    public string? CatalogError { get; set; }
    public List<ReviewItem> LatestReviews { get; set; } = [];
}

public class SearchViewModel
{
    public string Query { get; set; } = "";
    public string Tab { get; set; } = "peliculas";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public List<TitleCard> Movies { get; set; } = [];
    public List<TitleCard> Series { get; set; } = [];
    public List<TitleCard> Anime { get; set; } = [];
    public string? CatalogError { get; set; }

    public List<TitleCard> Current => Tab switch
    {
        "series" => Series,
        "anime" => Anime,
        _ => Movies,
    };
}

public class TitleViewModel
{
    public required TitleDetail Detail { get; init; }
    public required int TitleId { get; init; }
    public double? CommunityAverage { get; init; }
    public int CommunityCount { get; init; }
    public List<ReviewItem> Reviews { get; init; } = [];
    public ReviewItem? MyReview { get; init; }
    public BookmarkStatus? MyBookmark { get; init; }
    public bool YouTubeEnabled { get; init; }
    /// <summary>False si todavía no se estrenó: no se puede reseñar.</summary>
    public bool IsReleased { get; init; } = true;
}

public class ReviewFormViewModel
{
    public TmdbType Type { get; set; }
    public int TmdbId { get; set; }
    public string TitleName { get; set; } = "";
    public int? TitleYear { get; set; }
    public string? TitlePoster { get; set; }
    public bool IsEdit { get; set; }

    [Required(ErrorMessage = "Elegí un puntaje.")]
    [Range(1, 10, ErrorMessage = "El puntaje va de 1 a 10.")]
    [Display(Name = "Puntaje")]
    public int? Rating { get; set; }

    [Required(ErrorMessage = "Escribí tu review.")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Entre 10 y 5000 caracteres.")]
    [Display(Name = "Review")]
    public string Body { get; set; } = "";

    [Display(Name = "Contiene spoilers")]
    public bool HasSpoilers { get; set; }
}

public class VideosViewModel
{
    public List<YouTubeVideo> Videos { get; set; } = [];
    public string? Error { get; set; }
}

// ---------- Usuarios y listas ----------

public record BookmarkItem(TmdbType Type, int TmdbId, string Name, int? Year, string? PosterPath, bool IsAnime, BookmarkStatus Status, DateTime CreatedAt);

public class UserProfileViewModel
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsMe { get; set; }
    public List<ReviewItem> Reviews { get; set; } = [];
    public List<BookmarkItem> Bookmarks { get; set; } = [];
}

public class MyListsViewModel
{
    public string Status { get; set; } = "todos";
    public Dictionary<BookmarkStatus, int> Counts { get; set; } = [];
    public List<BookmarkItem> Items { get; set; } = [];
}

// ---------- API (fetch) ----------

public record BookmarkRequest(string Tipo, int TmdbId, BookmarkStatus? Status);
