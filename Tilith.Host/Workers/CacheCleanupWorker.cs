using Tilith.Core.Services;

namespace Tilith.Host.Workers;

public sealed class CacheCleanupWorker : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);
    private readonly LevelCacheService _cache;
    private readonly ILogger<CacheCleanupWorker> _logger;

    public CacheCleanupWorker(LevelCacheService cache, ILogger<CacheCleanupWorker> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache cleanup worker started (interval: {Interval}, sliding: 5m)", CleanupInterval);

        using var timer = new PeriodicTimer(CleanupInterval);

        while ( !stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken) )
        {
            var removed = _cache.EvictExpired();
            var (total, active) = _cache.GetStatistics();

            _logger.LogInformation(
                "Cache cleanup completed: {Removed} evicted, {Active}/{Total} active entries",
                removed, active, total
            );
        }
    }
}