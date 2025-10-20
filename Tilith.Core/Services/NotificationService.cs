using System.Threading.Channels;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class NotificationService
{
    private readonly Channel<LevelUpNotification> _channel = Channel.CreateUnbounded<LevelUpNotification>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        }
    );

    public ChannelReader<LevelUpNotification> Reader => _channel.Reader;

    public void QueueLevelUp(ulong userId, ulong channelId, int oldLevel, int newLevel)
    {
        _channel.Writer.TryWrite(new LevelUpNotification(userId, channelId, oldLevel, newLevel));
    }
}