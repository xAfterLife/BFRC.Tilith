using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Tilith.Api.Controllers;
using Tilith.Bot;
using Tilith.Core.Data;
using Tilith.Core.Services;
using Tilith.Host.HealthChecks;
using Tilith.Host.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddDbContextPool<TilithDbContext>(
    options => options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            npgsqlOptions.CommandTimeout(10);
            npgsqlOptions.MigrationsAssembly("Tilith.Core");
        }
    ),
    128
);

builder.Services.AddSingleton(new XpService(TimeSpan.FromMinutes(1)));
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<GemService>();
builder.Services.AddSingleton<UnitService>();
builder.Services.AddSingleton<TilithBot>();

// Register workers LAST (they'll start after Build())
builder.Services.AddHostedService<XpProcessor>();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddHostedService<DiscordBotWorker>();

builder.Services.AddControllers().AddApplicationPart(typeof(LeaderboardController).Assembly);
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHealthChecks()
       .AddDbContextCheck<TilithDbContext>()
       .AddCheck<DiscordHealthCheck>("discord");

// Add CORS
builder.Services.AddCors(options =>
    {
        options.AddPolicy("WebDashboard", policy =>
            {
                var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                policy.WithOrigins(origins)
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
        );
    }
);

var app = builder.Build();

using ( var scope = app.Services.CreateScope() )
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

    try
    {
        logger.LogInformation("Ensuring database exists and applying migrations...");

        // Check if database exists
        var canConnect = await db.Database.CanConnectAsync();
        if ( !canConnect )
        {
            logger.LogWarning("Database unreachable, waiting 5 seconds...");
            await Task.Delay(5000); // Give Postgres time to start in Docker
        }

        // Apply migrations
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        var migrations = pendingMigrations as string[] ?? pendingMigrations.ToArray();
        if ( migrations.Length != 0 )
        {
            logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                migrations.Length, string.Join(", ", migrations)
            );
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully");
        }
        else
        {
            logger.LogInformation("Database is up to date");
        }

        // Verify table exists
        var userCount = await db.Users.CountAsync();
        logger.LogInformation("Database ready. Current users: {Count}", userCount);
    }
    catch ( Exception ex )
    {
        logger.LogCritical(ex, "FATAL: Database migration failed. Application cannot start.");
        throw; // Crash the app to surface the issue in Docker logs
    }
}

app.UseCors("WebDashboard");
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
       {
           ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
           AllowCachingResponses = true
       }
   )
   .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(5)));

await app.RunAsync();