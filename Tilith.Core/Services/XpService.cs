using System.Threading.Channels;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class XpService
{
    private readonly XpCooldownTracker _cooldownTracker;
    private readonly LevelCacheService _levelCache;
    private readonly Channel<XpGrant> _xpChannel;

    public XpService(TimeSpan cooldown, LevelCacheService levelCache)
    {
        _cooldownTracker = new XpCooldownTracker(cooldown);
        _levelCache = levelCache;
        _xpChannel = Channel.CreateUnbounded<XpGrant>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            }
        );
    }

    public ChannelReader<XpGrant> Reader => _xpChannel.Reader;

    /// <summary>
    ///     Attempts to grant XP asynchronously. Fetches current level from cache.
    /// </summary>
    public async ValueTask<bool> TryGrantXpAsync(ulong userId, ulong channelId, DateTime timestamp, MessageMetadata metadata, CancellationToken cancellationToken = default)
    {
        if ( !_cooldownTracker.TryConsumeXpCooldown(userId, timestamp) )
            return false;

        var currentLevel = await _levelCache.GetUserLevelAsync(userId, cancellationToken);
        var xpAmount = LevelCalculator.CalculateXpGain(currentLevel);

        _xpChannel.Writer.TryWrite(new XpGrant(userId, channelId, xpAmount, timestamp, metadata));
        return true;
    }
}