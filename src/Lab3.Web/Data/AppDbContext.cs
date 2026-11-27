using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Data;

// Fase 1: contexto vacío. Las entidades (Titles, Reviews, etc.) se agregan en su fase.
// En la Fase 2 pasa a heredar de IdentityDbContext.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
