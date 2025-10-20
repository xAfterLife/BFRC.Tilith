using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Tilith.Core.Models;

namespace Tilith.Core.Entities;

public sealed class User
{
    [Key]
    public ulong DiscordId { get; init; }

    public int Gems { get; set; }
    public long Experience { get; set; }

    [NotMapped]
    public int Level => LevelCalculator.CalculateLevel(Experience);

    public DateTime LastDailyClaimUtc { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}