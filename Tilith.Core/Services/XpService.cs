using System.Threading.Channels;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class XpService
{
    private const long XpPerMessage = 10;
    private readonly XpCooldownTracker _cooldownTracker;
    private readonly Channel<XpGrant> _xpChannel;

    public XpService(TimeSpan cooldown)
    {
        _xpChannel = Channel.CreateUnbounded<XpGrant>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            }
        );
        _cooldownTracker = new XpCooldownTracker(cooldown);
    }

    public ChannelReader<XpGrant> Reader => _xpChannel.Reader;

    public bool TryGrantXp(ulong userId, ulong channelId, DateTime timestamp)
    {
        if ( !_cooldownTracker.TryConsumeXpCooldown(userId, timestamp) )
            return false;

        _xpChannel.Writer.TryWrite(new XpGrant(userId, channelId, XpPerMessage, timestamp));
        return true;
    }
}