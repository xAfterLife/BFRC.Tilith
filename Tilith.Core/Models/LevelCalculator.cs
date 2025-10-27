using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tilith.Core.Models;

public static class LevelCalculator
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly long[] CumulativeXp;
    private static readonly int MaxLevel;

    static LevelCalculator()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Tilith.Core.Resource.brave_frontier_levels.json") ?? throw new InvalidOperationException("brave_frontier_levels.json not found");

        var levels = JsonSerializer.Deserialize<LevelData[]>(stream, Options) ?? throw new InvalidOperationException("Failed to deserialize levels");

        MaxLevel = levels.Length;
        CumulativeXp = new long[MaxLevel + 1];

        var span = CumulativeXp.AsSpan();
        span[0] = 0;

        for ( var i = 0; i < MaxLevel; i++ )
        {
            span[i + 1] = span[i] + levels[i].XpRequired;
        }
    }

    public static int CalculateLevel(long experience)
    {
        if ( experience < 0 )
            return 1;

        var span = CumulativeXp.AsSpan();
        var idx = span.BinarySearch(experience);
        if ( idx >= 0 )
            return Math.Clamp(idx + 1, 1, MaxLevel);

        idx = Math.Max(0, ~idx - 1);
        return Math.Clamp(idx + 1, 1, MaxLevel);
    }

    public static long GetXpForLevel(int level)
    {
        if ( level <= 1 )
            return 0;
        return level > MaxLevel + 1 ? CumulativeXp[^1] : CumulativeXp[level - 1];
    }

    public static (int Level, long CurrentLevelXp, long NextLevelXp) GetLevelProgress(long experience)
    {
        var level = CalculateLevel(experience);
        var currentLevelXp = GetXpForLevel(level);
        var nextLevelXp = GetXpForLevel(level + 1);

        if ( nextLevelXp <= currentLevelXp )
            nextLevelXp = currentLevelXp + 1;

        return (level, currentLevelXp, nextLevelXp);
    }

    public static long CalculateXpGain(int currentLevel)
    {
        return Math.Clamp((long)Math.Ceiling(9 + currentLevel / 2.0), 10, 50);
    }
}