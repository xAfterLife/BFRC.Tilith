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

// Database
builder.Services.AddDbContext<TilithDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
);

// Core services (singletons for shared state)
builder.Services.AddSingleton(new XpService(TimeSpan.FromMinutes(1)));
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddScoped<GemService>();

// Workers
builder.Services.AddHostedService<XpProcessor>();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddSingleton<TilithBot>();
builder.Services.AddHostedService<DiscordBotWorker>();

// API
builder.Services.AddControllers().AddApplicationPart(typeof(LeaderboardController).Assembly); // Explicitly load API assembly
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHealthChecks()
       .AddDbContextCheck<TilithDbContext>()
       .AddCheck<DiscordHealthCheck>("discord");

// CORS for future web dashboard
builder.Services.AddCors(options =>
    {
        options.AddPolicy("WebDashboard", policy =>
            {
                policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
        );
    }
);

var app = builder.Build();

// Ensure DB created
using ( var scope = app.Services.CreateScope() )
{
    var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();
    await db.Database.MigrateAsync();
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