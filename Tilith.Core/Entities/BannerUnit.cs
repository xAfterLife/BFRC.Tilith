namespace Tilith.Core.Entities;

public sealed class BannerUnit
{
    public int BannerId { get; set; }

    public required string UnitId { get; set; }

    public decimal? RateUpMultiplier { get; set; }

    // Navigation
    public Banner Banner { get; set; } = null!;
}