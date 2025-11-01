using System.Threading.Channels;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class LevelUpNotificationService
{
    private readonly Channel<LevelUpNotification> _channel = Channel.CreateUnbounded<LevelUpNotification>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        }
    );

    public ChannelReader<LevelUpNotification> Reader => _channel.Reader;

    public void QueueLevelUp(ulong userId, ulong channelId, ulong guildId, long experience)
    {
        _channel.Writer.TryWrite(new LevelUpNotification(userId, channelId, guildId, experience));
    }
}