namespace Tilith.Core.Entities;

public sealed class UserUnitInstance
{
    public int Id { get; init; }
    public ulong DiscordId { get; init; }
    public int UnitId { get; set; }
    public long UnitXp { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; init; } = null!;
}