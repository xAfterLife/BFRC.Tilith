namespace Tilith.Core.Entities;

public sealed class UserInventory
{
    public int Id { get; init; }
    public ulong DiscordId { get; init; }
    public string UnitId { get; init; } = null!;
    public int Quantity { get; set; } = 1;
    public DateTime AcquiredAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; init; } = null!;
}