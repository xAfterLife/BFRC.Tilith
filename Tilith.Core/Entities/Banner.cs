namespace Tilith.Core.Entities;

public sealed class Banner
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTime StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public bool IsActive { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation
    public ICollection<BannerUnit> BannerUnits { get; set; } = new List<BannerUnit>();
}