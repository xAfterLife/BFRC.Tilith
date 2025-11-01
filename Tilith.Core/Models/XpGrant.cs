namespace Tilith.Core.Models;

public readonly record struct XpGrant
(
    ulong UserId,
    ulong ChannelId,
    ulong GuildId,
    long XpAmount,
    DateTime Timestamp,
    MessageMetadata MessageMetadata
);