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
    private readonly EvolutionService _evolutionService;
    private readonly LevelCacheService _levelCache;
    private readonly LevelUpNotificationService _levelUpNotificationService;
    private readonly ILogger<XpProcessor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XpService _xpService;

    public XpProcessor(XpService xpService,
        LevelUpNotificationService levelUpNotificationService,
        IServiceScopeFactory scopeFactory,
        LevelCacheService levelCache,
        EvolutionService evolutionService,
        ILogger<XpProcessor> logger)
    {
        _xpService = xpService;
        _levelUpNotificationService = levelUpNotificationService;
        _scopeFactory = scopeFactory;
        _levelCache = levelCache;
        _evolutionService = evolutionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("XP Processor started (with unit XP + evolution)");

        await foreach ( var batch in ReadBatchesAsync(stoppingToken) )
        {
            if ( batch.Count == 0 )
                continue;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

            var userGrants = batch
                             .GroupBy(x => x.UserId)
                             .Select(g => new
                                 {
                                     UserId = g.Key,
                                     XpDelta = g.Sum(x => x.XpAmount),
                                     g.Last().ChannelId,
                                     g.Last().GuildId,
                                     g.Last().MessageMetadata
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

                    continue;
                }

                var oldLevel = LevelCalculator.CalculateLevel(user.Experience);
                user.Experience += grant.XpDelta;

                if ( !string.IsNullOrEmpty(grant.MessageMetadata.Username) && user.Username != grant.MessageMetadata.Username )
                {
                    user.Username = grant.MessageMetadata.Username;
                    user.UpdatedAtUtc = now;
                }

                if ( !string.IsNullOrEmpty(grant.MessageMetadata.DisplayName) && user.DisplayName != grant.MessageMetadata.DisplayName )
                {
                    user.DisplayName = grant.MessageMetadata.DisplayName;
                    user.UpdatedAtUtc = now;
                }

                var newLevel = LevelCalculator.CalculateLevel(user.Experience);
                if ( newLevel > oldLevel )
                {
                    _levelUpNotificationService.QueueLevelUp(user.DiscordId, grant.ChannelId, grant.GuildId, user.Experience);
                    _levelCache.UpdateUserLevel(user.DiscordId, newLevel);
                }

                await _evolutionService.GrantUnitXpAsync(grant.UserId, stoppingToken);
                if ( await _evolutionService.TryEvolveAsync(grant.UserId, stoppingToken) )
                {
                    // Do something Fancy like the levelUpNotificationService above
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

            yield return [.. batch];
            batch.Clear();
        }
    }
}