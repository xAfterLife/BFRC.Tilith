namespace Tilith.Core.Entities;

public sealed class SummonHistory
{
    public long Id { get; init; }
    public ulong DiscordId { get; init; }
    public int? BannerId { get; init; }
    public string UnitId { get; init; } = null!;
    public int RarityPulled { get; init; }
    public int GemCost { get; init; }
    public DateTime SummonedAtUtc { get; init; }

    public User User { get; init; } = null!;
    public Banner? Banner { get; init; }
}