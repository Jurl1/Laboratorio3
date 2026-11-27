using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lab3.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Services;

/// <summary>Cache de respuestas externas en la tabla ApiCache (PLAN.md §5).</summary>
public sealed class ApiCache(AppDbContext db, TimeProvider clock, ILogger<ApiCache> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var entry = await db.ApiCache.FindAsync([key], ct);

        if (entry is not null && entry.ExpiresAt > now)
        {
            try
            {
                var cached = JsonSerializer.Deserialize<T>(entry.Payload, Json);
                if (cached is not null) return cached;
            }
            catch (JsonException ex)
            {
                // Cambió el formato del DTO: se vuelve a pedir.
                logger.LogInformation(ex, "Cache inválido para {Key}", key);
            }
        }

        var value = await factory(ct);
        var payload = JsonSerializer.Serialize(value, Json);

        if (entry is null)
        {
            db.ApiCache.Add(new ApiCacheEntry { Key = key, Payload = payload, ExpiresAt = now + ttl });
        }
        else
        {
            entry.Payload = payload;
            entry.ExpiresAt = now + ttl;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Otro request guardó la misma clave al mismo tiempo: no pasa nada.
            logger.LogDebug(ex, "Carrera al guardar cache {Key}", key);
            db.ChangeTracker.Clear();
        }

        return value;
    }

    /// <summary>Clave corta y estable para textos libres (ej: búsquedas).</summary>
    public static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
