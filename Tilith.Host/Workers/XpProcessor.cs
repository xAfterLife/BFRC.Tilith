using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Tilith.Core.Data;
using Tilith.Core.Entities;
using Tilith.Core.Models;
using Tilith.Core.Services;

namespace Tilith.Host.Workers;

public sealed class XpProcessor : BackgroundService
{
    private static readonly TimeSpan BatchInterval = TimeSpan.FromSeconds(5);
    private readonly LevelCacheService _levelCache;
    private readonly ILogger<XpProcessor> _logger;
    private readonly NotificationService _notificationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XpService _xpService;

    public XpProcessor(XpService xpService,
        NotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        LevelCacheService levelCache,
        ILogger<XpProcessor> logger)
    {
        _xpService = xpService;
        _notificationService = notificationService;
        _scopeFactory = scopeFactory;
        _levelCache = levelCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("XP Processor started with username tracking");

        await foreach ( var batch in ReadBatchesAsync(stoppingToken) )
        {
            if ( batch.Count == 0 )
                continue;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

            // Group grants + capture message metadata
            var userGrants = batch
                             .GroupBy(x => x.UserId)
                             .Select(g => new
                                 {
                                     UserId = g.Key,
                                     XpDelta = g.Sum(x => x.XpAmount),
                                     g.Last().ChannelId,
                                     g.Last().MessageMetadata // NEW: username/display from message
                                 }
                             )
                             .ToList();

            var userIds = userGrants.Select(x => x.UserId).ToArray();
            var users = await context.Users
                                     .Where(u => userIds.Contains(u.DiscordId))
                                     .ToDictionaryAsync(u => u.DiscordId, stoppingToken);

            var now = DateTime.UtcNow;

            foreach ( var grant in userGrants )
            {
                if ( !users.TryGetValue(grant.UserId, out var user) )
                {
                    // New user: capture initial username
                    user = new User
                    {
                        DiscordId = grant.UserId,
                        Experience = grant.XpDelta,
                        Username = grant.MessageMetadata.Username,
                        DisplayName = grant.MessageMetadata.DisplayName,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                    context.Users.Add(user);
                    users[grant.UserId] = user;
                }
                else
                {
                    var oldLevel = LevelCalculator.CalculateLevel(user.Experience);
                    user.Experience += grant.XpDelta;

                    // Update names if changed (opportunistic sync)
                    var newUsername = grant.MessageMetadata.Username;
                    var newDisplayName = grant.MessageMetadata.DisplayName;

                    if ( !string.IsNullOrEmpty(newUsername) && user.Username != newUsername )
                    {
                        user.Username = newUsername;
                        user.UpdatedAtUtc = now;
                    }

                    if ( !string.IsNullOrEmpty(newDisplayName) && user.DisplayName != newDisplayName )
                    {
                        user.DisplayName = newDisplayName;
                        user.UpdatedAtUtc = now;
                    }

                    var newLevel = LevelCalculator.CalculateLevel(user.Experience);
                    if ( newLevel <= oldLevel )
                        continue;

                    _notificationService.QueueLevelUp(user.DiscordId, grant.ChannelId, oldLevel, newLevel);
                    _levelCache.UpdateUserLevel(user.DiscordId, newLevel);
                }
            }

            await context.SaveChangesAsync(stoppingToken);
        }
    }

    private async IAsyncEnumerable<List<XpGrant>> ReadBatchesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<XpGrant>(256);
        var reader = _xpService.Reader;

        while ( !cancellationToken.IsCancellationRequested )
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(BatchInterval);

            try
            {
                while ( await reader.WaitToReadAsync(cts.Token) && reader.TryRead(out var grant) )
                {
                    batch.Add(grant);
                    if ( batch.Count >= 256 )
                        break;
                }
            }
            catch ( OperationCanceledException ) when ( !cancellationToken.IsCancellationRequested ) { }

            if ( batch.Count <= 0 )
                continue;

            yield return [..batch];
            batch.Clear();
        }
    }
}