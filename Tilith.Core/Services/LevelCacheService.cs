using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tilith.Core.Data;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class LevelCacheService
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<ulong, CacheEntry> _cache = new();
    private readonly ILogger<LevelCacheService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public LevelCacheService(IServiceScopeFactory scopeFactory, ILogger<LevelCacheService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Gets user's current level with sliding expiration (read refreshes TTL).
    /// </summary>
    public async ValueTask<int> GetUserLevelAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Fast path: cache hit with sliding expiration
        if ( _cache.TryGetValue(userId, out var entry) )
        {
            if ( entry.ExpiresAt > now )
            {
                // Refresh expiration on read (sliding window)
                entry.RefreshExpiration(SlidingExpiration);
                return entry.Level;
            }

            // Expired entry - remove and refetch
            _cache.TryRemove(userId, out _);
        }

        // Slow path: fetch from DB
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var experience = await db.Users
                                 .AsNoTracking()
                                 .Where(u => u.DiscordId == userId)
                                 .Select(u => u.Experience)
                                 .FirstOrDefaultAsync(cancellationToken);

        var level = experience > 0 ? LevelCalculator.CalculateLevel(experience) : 1;

        // Create new entry with sliding expiration
        var newEntry = new CacheEntry(level, now.Add(SlidingExpiration));
        _cache[userId] = newEntry;

        _logger.LogDebug("Cached level {Level} for user {UserId} (sliding 5m)", level, userId);
        return level;
    }

    /// <summary>
    ///     Explicitly invalidates cache entry (call after level-ups or admin modifications).
    /// </summary>
    public void InvalidateUser(ulong userId)
    {
        if ( _cache.TryRemove(userId, out _) )
        {
            _logger.LogDebug("Invalidated cache for user {UserId}", userId);
        }
    }

    /// <summary>
    ///     Updates cache directly with fresh TTL (call after level-ups to avoid stale reads).
    /// </summary>
    public void UpdateUserLevel(ulong userId, int newLevel)
    {
        var entry = new CacheEntry(newLevel, DateTime.UtcNow.Add(SlidingExpiration));
        _cache[userId] = entry;
        _logger.LogDebug("Updated cache for user {UserId} to level {Level}", userId, newLevel);
    }

    /// <summary>
    ///     Evicts expired entries in bulk (optional background cleanup).
    /// </summary>
    public int EvictExpired()
    {
        var now = DateTime.UtcNow;
        var toRemove = new List<ulong>(32);

        foreach ( var (key, entry) in _cache )
        {
            if ( entry.ExpiresAt <= now )
            {
                toRemove.Add(key);
            }
        }

        var removed = 0;
        foreach ( var key in toRemove )
        {
            if ( _cache.TryRemove(key, out _) )
            {
                removed++;
            }
        }

        if ( removed > 0 )
        {
            _logger.LogDebug("Evicted {Count} expired cache entries", removed);
        }

        return removed;
    }

    /// <summary>
    ///     Returns current cache statistics (for health checks/monitoring).
    /// </summary>
    public (int TotalEntries, int ActiveEntries) GetStatistics()
    {
        var now = DateTime.UtcNow;
        var total = _cache.Count;
        var active = _cache.Count(kvp => kvp.Value.ExpiresAt > now);
        return (total, active);
    }

    private sealed class CacheEntry
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private DateTime _expiresAt;

        public CacheEntry(int level, DateTime expiresAt)
        {
            Level = level;
            _expiresAt = expiresAt;
        }

        public int Level { get; }

        public DateTime ExpiresAt
        {
            get
            {
                _lock.EnterReadLock();
                var value = _expiresAt;
                _lock.ExitReadLock();
                return value;
            }
        }

        /// <summary>
        ///     Atomically refreshes expiration (sliding window).
        /// </summary>
        public void RefreshExpiration(TimeSpan slidingWindow)
        {
            _lock.EnterWriteLock();
            _expiresAt = DateTime.UtcNow.Add(slidingWindow);
            _lock.ExitWriteLock();
        }
    }
}