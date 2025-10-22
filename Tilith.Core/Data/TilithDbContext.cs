using Microsoft.EntityFrameworkCore;
using Tilith.Core.Entities;

namespace Tilith.Core.Data;

public sealed class TilithDbContext : DbContext
{
    public TilithDbContext(DbContextOptions<TilithDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<BannerUnit> BannerUnits => Set<BannerUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.DiscordId);
                entity.HasIndex(e => e.DiscordId)
                      .IsUnique();

                entity.HasIndex(e => e.Experience);
                entity.HasIndex(e => e.Gems);

                entity.Property(e => e.DiscordId)
                      .IsRequired();

                entity.Property(e => e.Username)
                      .HasMaxLength(32);

                entity.Property(e => e.DisplayName)
                      .HasMaxLength(32);

                entity.Property(e => e.CreatedAtUtc)
                      .HasDefaultValueSql("NOW()");

                entity.Property(e => e.UpdatedAtUtc)
                      .HasDefaultValueSql("NOW()")
                      .ValueGeneratedOnAddOrUpdate();
            }
        );

        modelBuilder.Entity<Banner>(entity =>
            {
                entity.ToTable("Banners");
                entity.HasKey(b => b.Id);

                entity.Property(b => b.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(b => b.Description)
                      .HasMaxLength(500);

                entity.Property(b => b.ImageUrl)
                      .HasMaxLength(2048);

                entity.Property(b => b.CreatedAtUtc)
                      .HasDefaultValueSql("NOW()");

                entity.Property(b => b.UpdatedAtUtc)
                      .HasDefaultValueSql("NOW()")
                      .ValueGeneratedOnAddOrUpdate();

                // Indexes for active banner queries
                entity.HasIndex(b => b.IsActive);
                entity.HasIndex(b => b.StartDateUtc);
                entity.HasIndex(b => b.EndDateUtc);
            }
        );

        modelBuilder.Entity<BannerUnit>(entity =>
            {
                entity.ToTable("BannerUnits");
                entity.HasKey(bu => new { bu.BannerId, bu.UnitId });

                entity.Property(bu => bu.UnitId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(bu => bu.RateUpMultiplier)
                      .HasPrecision(4, 2);

                entity.HasOne(bu => bu.Banner)
                      .WithMany(b => b.BannerUnits)
                      .HasForeignKey(bu => bu.BannerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(bu => bu.UnitId);
            }
        );
    }
}