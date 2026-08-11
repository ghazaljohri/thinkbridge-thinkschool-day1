using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;
using QuotesApi.Models.Collections;
using QuotesApi.Models.Auth;

namespace QuotesApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.HasIndex(u => u.Email)
                .IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Token)
                .IsRequired();

            entity.HasIndex(r => r.Token)
                .IsUnique();

            entity.Property(r => r.ExpiresAt)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(c => c.OwnerId)
                .IsRequired();

            entity.HasMany(c => c.Items)
                .WithOne()
                .HasForeignKey(i => i.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CollectionItem>(entity =>
        {
            entity.HasKey(i => new { i.CollectionId, i.QuoteId });

            entity.Property(i => i.AddedAt)
                .IsRequired();

            entity.HasOne<Quote>()
                .WithMany()
                .HasForeignKey(i => i.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
