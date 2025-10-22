namespace Tilith.Core.Models;

public readonly record struct XpGrant
(
    ulong UserId,
    ulong ChannelId,
    long XpAmount,
    DateTime Timestamp,
    MessageMetadata MessageMetadata
);