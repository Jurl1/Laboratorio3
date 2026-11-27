using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewLike> ReviewLikes => Set<ReviewLike>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<ApiCacheEntry> ApiCache => Set<ApiCacheEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(50);
            e.Property(u => u.AvatarUrl).HasMaxLength(500);
            e.Property(u => u.Bio).HasMaxLength(500);
        });

        builder.Entity<Title>(e =>
        {
            e.HasIndex(t => new { t.TmdbType, t.TmdbId }).IsUnique();
            e.Property(t => t.Name).HasMaxLength(300);
            e.Property(t => t.OriginalName).HasMaxLength(300);
            e.Property(t => t.ImdbId).HasMaxLength(20);
            e.Property(t => t.PosterPath).HasMaxLength(200);
            e.Property(t => t.BackdropPath).HasMaxLength(200);
            e.Property(t => t.TrailerYoutubeKey).HasMaxLength(50);
            e.Property(t => t.GenresJson).HasColumnType("jsonb");
        });

        builder.Entity<Review>(e =>
        {
            e.HasIndex(r => new { r.UserId, r.TitleId }).IsUnique();
            e.HasIndex(r => r.CreatedAt);
            e.Property(r => r.Body).HasMaxLength(5000);
            e.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 10"));
            e.HasOne(r => r.User).WithMany(u => u.Reviews).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Title).WithMany(t => t.Reviews).HasForeignKey(r => r.TitleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReviewLike>(e =>
        {
            e.HasKey(l => new { l.UserId, l.ReviewId });
            e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Review).WithMany(r => r.Likes).HasForeignKey(l => l.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Bookmark>(e =>
        {
            e.HasKey(b => new { b.UserId, b.TitleId });
            e.HasOne(b => b.User).WithMany(u => u.Bookmarks).HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Title).WithMany(t => t.Bookmarks).HasForeignKey(b => b.TitleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApiCacheEntry>(e =>
        {
            e.ToTable("ApiCache");
            e.HasKey(c => c.Key);
            e.Property(c => c.Key).HasMaxLength(200);
            e.Property(c => c.Payload).HasColumnType("jsonb");
            e.HasIndex(c => c.ExpiresAt);
        });
    }
}
