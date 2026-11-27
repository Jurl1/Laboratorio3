using Microsoft.AspNetCore.Identity;

namespace Lab3.Web.Services;

/// <summary>Mensajes de Identity en español.</summary>
public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => E(nameof(DefaultError), "Ocurrió un error.");
    public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Los datos cambiaron mientras los editabas. Probá de nuevo.");
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "La contraseña actual no es correcta.");
    public override IdentityError InvalidToken() => E(nameof(InvalidToken), "El enlace no es válido o venció.");
    public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Ya existe un usuario con ese inicio de sesión.");
    public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), "El nombre de usuario solo puede tener letras, números, punto, guion y guion bajo.");
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), "El mail no es válido.");
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), $"El usuario \"{userName}\" ya está en uso.");
    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), "Ese mail ya está registrado.");
    public override IdentityError InvalidRoleName(string? role) => E(nameof(InvalidRoleName), "Rol inválido.");
    public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), "El rol ya existe.");
    public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "El usuario ya tiene contraseña.");
    public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "El bloqueo no está habilitado para este usuario.");
    public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), "El usuario ya tiene ese rol.");
    public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), "El usuario no tiene ese rol.");
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), $"La contraseña debe tener al menos {uniqueChars} caracteres distintos.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe tener al menos un símbolo.");
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "La contraseña debe tener al menos un número.");
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "La contraseña debe tener al menos una minúscula.");
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "La contraseña debe tener al menos una mayúscula.");

    private static IdentityError E(string code, string description) => new() { Code = code, Description = description };
}
