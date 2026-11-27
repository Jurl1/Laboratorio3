using Lab3.Web.Data;
using Lab3.Web.Infrastructure;
using Lab3.Web.Models;
using Lab3.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lab3.Web.Controllers;

[Route("cuenta")]
public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IEmailSender email,
    ILogger<AccountController> logger) : Controller
{
    // ---------- Registro ----------

    [HttpGet("registro")]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost("registro")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim(),
            DisplayName = model.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        logger.LogInformation("Usuario nuevo {UserName}", user.UserName);
        await signIn.SignInAsync(user, isPersistent: false);
        TempData["Flash"] = $"¡Bienvenido/a, {user.DisplayName}!";
        return LocalRedirectOrHome(returnUrl);
    }

    // ---------- Login / logout ----------

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirectOrHome(returnUrl);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Se puede entrar con usuario o con mail.
        var login = model.Login.Trim();
        var user = login.Contains('@') ? await users.FindByEmailAsync(login) : await users.FindByNameAsync(login);

        if (user is not null)
        {
            var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded) return LocalRedirectOrHome(model.ReturnUrl);
            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Demasiados intentos fallidos. Probá de nuevo en 15 minutos.");
                return View(model);
            }
        }

        // Mismo mensaje exista o no el usuario.
        ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
        return View(model);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ---------- Perfil ----------

    [Authorize]
    [HttpGet("perfil")]
    public async Task<IActionResult> Profile()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new ProfileViewModel
        {
            UserName = user.UserName!,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
        });
    }

    [Authorize]
    [HttpPost("perfil")]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        model.UserName = user.UserName!;
        model.Email = user.Email!;
        if (!ModelState.IsValid) return View(model);

        user.DisplayName = model.DisplayName.Trim();
        user.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        await users.UpdateAsync(user);

        TempData["Flash"] = "Perfil actualizado.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet("contrasena")]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost("contrasena")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await signIn.RefreshSignInAsync(user);
        TempData["Flash"] = "Contraseña cambiada.";
        return RedirectToAction(nameof(Profile));
    }

    // ---------- Olvidé mi contraseña (requiere SMTP) ----------

    [HttpGet("olvide")]
    public IActionResult ForgotPassword()
    {
        ViewData["SmtpEnabled"] = email.IsConfigured;
        return View();
    }

    [HttpPost("olvide")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["SmtpEnabled"] = email.IsConfigured;
        if (!email.IsConfigured || !ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action(nameof(ResetPassword), "Account", new { userId = user.Id, token }, Request.Scheme)!;
            try
            {
                await email.SendAsync(user.Email!, "Lab3: restablecer contraseña",
                    $"Hola {user.DisplayName}:\n\nPara elegir una contraseña nueva entrá a este enlace:\n{link}\n\n" +
                    "Si no lo pediste, ignorá este mail.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo mandar el mail de reset");
            }
        }

        // Mismo mensaje exista o no el mail.
        TempData["Flash"] = "Si el mail está registrado, te mandamos un enlace para restablecer la contraseña.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("restablecer")]
    public IActionResult ResetPassword(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) return RedirectToAction(nameof(Login));
        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [HttpPost("restablecer")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByIdAsync(model.UserId);
        var result = user is null
            ? IdentityResult.Failed(new SpanishIdentityErrorDescriber().InvalidToken())
            : await users.ResetPasswordAsync(user, model.Token, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        TempData["Flash"] = "Contraseña restablecida. Ya podés entrar.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult LocalRedirectOrHome(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
