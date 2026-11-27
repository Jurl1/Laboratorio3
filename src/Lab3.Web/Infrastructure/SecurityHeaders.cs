namespace Lab3.Web.Infrastructure;

public static class RateLimits
{
    public const string Auth = "auth";
}

/// <summary>Headers de seguridad y Content-Security-Policy (PLAN.md §8).</summary>
public static class SecurityHeaders
{
    // Solo se permiten imágenes de TMDB (pósters, logo) y miniaturas de YouTube,
    // y iframes de youtube-nocookie.com. Nada de scripts ni estilos externos o inline.
    public const string Csp =
        "default-src 'self'; " +
        "img-src 'self' data: https://image.tmdb.org https://www.themoviedb.org https://i.ytimg.com; " +
        "frame-src https://www.youtube-nocookie.com; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool enableCsp) =>
        app.Use(async (ctx, next) =>
        {
            ctx.Response.OnStarting(() =>
            {
                var h = ctx.Response.Headers;
                if (enableCsp) h.ContentSecurityPolicy = Csp;
                h.XContentTypeOptions = "nosniff";
                h.XFrameOptions = "DENY";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                return Task.CompletedTask;
            });
            await next();
        });
}
