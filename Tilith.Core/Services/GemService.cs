using Tilith.Core.Data;
using Tilith.Core.Entities;

namespace Tilith.Core.Services;

public sealed class GemService
{
    private const int DailyGemAmount = 5;
    private static readonly TimeSpan DailyCooldown = TimeSpan.FromHours(24);
    private readonly TilithDbContext _context;

    public GemService(TilithDbContext context)
    {
        _context = context;
    }

    public async ValueTask<(bool Success, int NewGemCount, TimeSpan? TimeUntilNext)> ClaimDailyGemsAsync(ulong discordId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync([discordId], cancellationToken);
        var now = DateTime.UtcNow;

        if ( user is null )
        {
            user = new User { DiscordId = discordId, Gems = DailyGemAmount, LastDailyClaimUtc = now };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return (true, user.Gems, null);
        }

        var timeSinceLast = now - user.LastDailyClaimUtc;
        if ( timeSinceLast < DailyCooldown )
        {
            return (false, user.Gems, DailyCooldown - timeSinceLast);
        }

        user.Gems += DailyGemAmount;
        user.LastDailyClaimUtc = now;
        await _context.SaveChangesAsync(cancellationToken);
        return (true, user.Gems, null);
    }
}