using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Tilith.Core.Models;

namespace Tilith.Core.Entities;

public sealed class User
{
    [Key]
    [Required]
    public ulong DiscordId { get; set; }

    [MaxLength(32)] // Discord username max: 32 + #0000
    public string? Username { get; set; }

    [MaxLength(32)] // Discord display name max
    public string? DisplayName { get; set; }

    public long Experience { get; set; }
    public int Gems { get; set; }

    public DateTime? LastDailyClaimUtc { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int? FavoriteUnitId { get; set; }

    [NotMapped]
    public int Level => LevelCalculator.CalculateLevel(Experience);
}