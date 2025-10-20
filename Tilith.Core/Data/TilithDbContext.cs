using Microsoft.EntityFrameworkCore;
using Tilith.Core.Entities;

namespace Tilith.Core.Data;

public sealed class TilithDbContext : DbContext
{
    public TilithDbContext(DbContextOptions<TilithDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Experience); // For leaderboard queries
            }
        );
    }
}