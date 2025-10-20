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
    private readonly ILogger<XpProcessor> _logger;
    private readonly NotificationService _notificationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XpService _xpService;

    public XpProcessor(XpService xpService,
        NotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        ILogger<XpProcessor> logger)
    {
        _xpService = xpService;
        _notificationService = notificationService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("XP Processor started");

        await foreach ( var batch in ReadBatchesAsync(stoppingToken) )
        {
            if ( batch.Count == 0 )
                continue;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

            // Group by user, keep last channel for notification
            var userGrants = batch
                             .GroupBy(x => x.UserId)
                             .Select(g => new
                                 {
                                     UserId = g.Key,
                                     XpDelta = g.Sum(x => x.XpAmount),
                                     g.Last().ChannelId // Use most recent channel for notification
                                 }
                             )
                             .ToList();

            var userIds = userGrants.Select(x => x.UserId).ToArray();
            var users = await context.Users
                                     .Where(u => userIds.Contains(u.DiscordId))
                                     .ToDictionaryAsync(u => u.DiscordId, stoppingToken);

            foreach ( var grant in userGrants )
            {
                if ( !users.TryGetValue(grant.UserId, out var user) )
                {
                    user = new User { DiscordId = grant.UserId, Experience = grant.XpDelta };
                    context.Users.Add(user);
                    users[grant.UserId] = user;
                }
                else
                {
                    var oldLevel = LevelCalculator.CalculateLevel(user.Experience);
                    user.Experience += grant.XpDelta;
                    var newLevel = LevelCalculator.CalculateLevel(user.Experience);

                    // Detect level-up
                    if ( newLevel <= oldLevel )
                        continue;

                    _notificationService.QueueLevelUp(user.DiscordId, grant.ChannelId, oldLevel, newLevel);
                    _logger.LogInformation("User {UserId} leveled up: {OldLevel} → {NewLevel}",
                        user.DiscordId, oldLevel, newLevel
                    );
                }
            }

            await context.SaveChangesAsync(stoppingToken);
            _logger.LogDebug("Processed {Count} XP grants for {UserCount} users",
                batch.Count, userGrants.Count
            );
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
            catch ( OperationCanceledException ) when ( !cancellationToken.IsCancellationRequested )
            {
                // Timeout - flush batch
            }

            if ( batch.Count <= 0 )
                continue;

            yield return [..batch];
            batch.Clear();
        }
    }
}