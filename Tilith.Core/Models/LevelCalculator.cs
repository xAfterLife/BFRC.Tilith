using System.Runtime.CompilerServices;

namespace Tilith.Core.Models;

public static class LevelCalculator
{
    private const long BaseXpPerLevel = 100;
    private const double ExponentialFactor = 1.15;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateLevel(long experience)
    {
        if ( experience < BaseXpPerLevel )
            return 1;

        // Exponential curve: XP for level N = BaseXP * (1.15^(N-1))
        // Inverse: Level = log(XP/BaseXP) / log(1.15) + 1
        var level = (int)(Math.Log(experience / (double)BaseXpPerLevel) / Math.Log(ExponentialFactor)) + 1;
        return Math.Max(1, level);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetXpForLevel(int level)
    {
        if ( level <= 1 )
            return 0;
        return (long)(BaseXpPerLevel * Math.Pow(ExponentialFactor, level - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int Level, long CurrentLevelXp, long NextLevelXp) GetLevelProgress(long experience)
    {
        var level = CalculateLevel(experience);
        var currentLevelXp = GetXpForLevel(level);
        var nextLevelXp = GetXpForLevel(level + 1);
        return (level, currentLevelXp, nextLevelXp);
    }
}