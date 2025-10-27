namespace Tilith.Core.Models;

public readonly record struct SummonResult
(
    UnitData Unit,
    int Rarity,
    bool IsRateUp,
    int GemsSpent,
    int RemainingGems
);