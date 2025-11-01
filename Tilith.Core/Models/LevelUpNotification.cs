namespace Tilith.Core.Models;

public readonly record struct LevelUpNotification
(
    ulong UserId,
    ulong ChannelId,
    ulong GuildId,
    long Experience
);