using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Tilith.Core.Data;

/// <summary>
///     Design-time factory for EF Core tooling (migrations, scaffolding).
///     Only invoked by dotnet-ef; production uses DI-registered DbContext.
/// </summary>
public sealed class TilithDbContextFactory : IDesignTimeDbContextFactory<TilithDbContext>
{
    public TilithDbContext CreateDbContext(string[] args)
    {
        // 1. Try environment variable first (CI/CD, tooling)
        var connectionString = Environment.GetEnvironmentVariable("TILITH_CONNECTION_STRING");

        // 2. Fallback to appsettings.json in current directory
        if ( string.IsNullOrWhiteSpace(connectionString) )
        {
            var configuration = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", true)
                                .AddJsonFile("appsettings.Development.json", true)
                                .Build();

            connectionString = configuration.GetConnectionString("Postgres");
        }

        // 3. Validate
        if ( string.IsNullOrWhiteSpace(connectionString) )
        {
            throw new InvalidOperationException(
                "No connection string found. Set TILITH_CONNECTION_STRING environment variable or add 'ConnectionStrings:Postgres' to appsettings.json"
            );
        }

        // 4. Build options for PostgreSQL
        var optionsBuilder = new DbContextOptionsBuilder<TilithDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.MigrationsAssembly("Tilith.Core");
            }
        );

        return new TilithDbContext(optionsBuilder.Options);
    }
}