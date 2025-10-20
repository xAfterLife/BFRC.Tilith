namespace Tilith.Core.Models;

public readonly record struct LevelUpNotification
(
    ulong UserId,
    ulong ChannelId,
    int OldLevel,
    int NewLevel
);