using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tilith.Core.Models;

public static class LevelCalculator
{
    private const int BaseXpValue = 4;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly long[] CumulativeXp;
    private static readonly long[] XpGains;
    private static readonly int MaxLevel;

    static LevelCalculator()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Tilith.Core.Resource.brave_frontier_levels.json") ?? throw new InvalidOperationException("brave_frontier_levels.json not found");

        var levels = JsonSerializer.Deserialize<LevelData[]>(stream, Options) ?? throw new InvalidOperationException("Failed to deserialize levels");

        MaxLevel = levels.Length;
        CumulativeXp = new long[MaxLevel + 1];
        XpGains = new long[MaxLevel + 1];

        var cumulativeSpan = CumulativeXp.AsSpan(1);
        var gainsSpan = XpGains.AsSpan(0, MaxLevel);

        long runningTotal = 0;
        for ( var i = 0; i < MaxLevel; i++ )
        {
            runningTotal += levels[i].XpRequired;
            cumulativeSpan[i] = runningTotal;
            gainsSpan[i] = BaseXpValue * (i + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateLevel(long experience)
    {
        if ( experience < 0 )
            return 1;

        var idx = CumulativeXp.AsSpan().BinarySearch(experience);
        var level = idx >= 0 ? idx + 1 : ~idx;
        return Math.Clamp(level, 1, MaxLevel);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetXpForLevel(int level)
    {
        if ( level <= 1 )
            return 0;
        return level > MaxLevel ? CumulativeXp[^1] : CumulativeXp[level - 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CalculateXpGain(int currentLevel)
    {
        return currentLevel > MaxLevel ? 0 : XpGains[currentLevel - 1];
    }
}