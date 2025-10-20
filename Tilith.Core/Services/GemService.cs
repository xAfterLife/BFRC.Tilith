using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tilith.Core.Data;
using Tilith.Core.Entities;

namespace Tilith.Core.Services;

public sealed class GemService
{
    private const int DailyGemAmount = 5;
    private static readonly TimeSpan DailyCooldown = TimeSpan.FromHours(24);
    private readonly ILogger<GemService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public GemService(IServiceScopeFactory scopeFactory, ILogger<GemService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask<(bool Success, int NewGemCount, TimeSpan? TimeUntilNext)> ClaimDailyGemsAsync(ulong discordId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var now = DateTime.UtcNow;

        try
        {
            var user = await context.Users.FindAsync([discordId], cancellationToken);

            if ( user is null )
            {
                user = new User
                {
                    DiscordId = discordId,
                    Gems = DailyGemAmount,
                    LastDailyClaimUtc = now
                };
                context.Users.Add(user);
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("New user {UserId} claimed first daily gems", discordId);
                return (true, user.Gems, null);
            }

            var timeSinceLast = now - user.LastDailyClaimUtc;
            if ( timeSinceLast < DailyCooldown )
            {
                return (false, user.Gems, DailyCooldown - timeSinceLast);
            }

            user.Gems += DailyGemAmount;
            user.LastDailyClaimUtc = now;
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("User {UserId} claimed daily gems (total: {Gems})", discordId, user.Gems);

            return (true, user.Gems, null);
        }
        catch ( Exception ex )
        {
            _logger.LogError(ex, "Failed to claim daily gems for user {UserId}", discordId);
            throw;
        }
    }
}