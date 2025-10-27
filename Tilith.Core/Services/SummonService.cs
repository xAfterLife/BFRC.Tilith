using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tilith.Core.Data;
using Tilith.Core.Entities;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class SummonService
{
    private const int BaseSummonCost = 5;

    private static readonly FrozenDictionary<int, double> BaseRarityRates = new Dictionary<int, double>
    {
        [3] = 0.50,
        [4] = 0.35,
        [5] = 0.12,
        [6] = 0.03
    }.ToFrozenDictionary();

    private readonly ILogger<SummonService> _logger;
    private readonly Random _rng = Random.Shared;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UnitService _unitService;

    public SummonService(IServiceScopeFactory scopeFactory,
        UnitService unitService,
        ILogger<SummonService> logger)
    {
        _scopeFactory = scopeFactory;
        _unitService = unitService;
        _logger = logger;
    }

    public async ValueTask<Result<SummonResult>> SummonAsync(ulong discordId,
        int? bannerId,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        // Use EF's execution strategy for retry-safe transactions
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var user = await db.Users.FindAsync([discordId], ct);
                    if ( user is null || user.Gems < BaseSummonCost )
                        return Result<SummonResult>.Failure("Insufficient gems (need 5)");

                    // Get banner pool (move outside transaction if possible)
                    var banner = bannerId.HasValue
                        ? await db.Banners
                                  .AsNoTracking()
                                  .Include(b => b.BannerUnits)
                                  .FirstOrDefaultAsync(b => b.Id == bannerId && b.IsActive, ct)
                        : null;

                    // Roll unit
                    var (unit, rarity, isRateUp) = RollUnit(banner);

                    // Deduct gems
                    user.Gems -= BaseSummonCost;
                    user.UpdatedAtUtc = DateTime.UtcNow;

                    // Upsert inventory
                    var inventory = await db.UserInventory
                                            .FirstOrDefaultAsync(i => i.DiscordId == discordId && i.UnitId == unit.UnitId, ct);

                    if ( inventory is null )
                    {
                        db.UserInventory.Add(new UserInventory
                            {
                                DiscordId = discordId,
                                UnitId = unit.UnitId,
                                Quantity = 1,
                                AcquiredAtUtc = DateTime.UtcNow,
                                UpdatedAtUtc = DateTime.UtcNow
                            }
                        );
                    }
                    else
                    {
                        inventory.Quantity++;
                        inventory.UpdatedAtUtc = DateTime.UtcNow;
                    }

                    // Log history
                    db.SummonHistory.Add(new SummonHistory
                        {
                            DiscordId = discordId,
                            BannerId = bannerId,
                            UnitId = unit.UnitId,
                            RarityPulled = rarity,
                            GemCost = BaseSummonCost,
                            SummonedAtUtc = DateTime.UtcNow
                        }
                    );

                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);

                    _logger.LogInformation(
                        "User {UserId} summoned {UnitId} (★{Rarity})",
                        discordId, unit.UnitId, rarity
                    );

                    return Result<SummonResult>.Success(new SummonResult(
                            unit, rarity, isRateUp, BaseSummonCost, user.Gems
                        )
                    );
                }
                catch ( Exception ex )
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogError(ex, "Summon failed for user {UserId}", discordId);
                    throw; // Let strategy handle retry
                }
            }
        );
    }

    private (UnitData Unit, int Rarity, bool IsRateUp) RollUnit(Banner? banner)
    {
        var rarity = RollRarity();

        // Filter units by rarity
        var pool = _unitService.Units
                               .Where(u => u.Rarity.StartsWith(rarity.ToString()))
                               .ToArray();

        if ( pool.Length == 0 )
            throw new InvalidOperationException($"No units of rarity {rarity}");

        // Rate-up logic
        var isRateUp = false;
        if ( banner is not null && banner.BannerUnits.Count > 0 )
        {
            var rateUpUnits = banner.BannerUnits
                                    .Select(bu => bu.UnitId)
                                    .ToHashSet();

            var rateUpPool = pool.Where(u => rateUpUnits.Contains(u.UnitId)).ToArray();

            // 30% chance to pull from rate-up pool if available
            if ( rateUpPool.Length > 0 && _rng.NextDouble() < 0.30 )
            {
                isRateUp = true;
                pool = rateUpPool;
            }
        }

        var unit = pool[_rng.Next(pool.Length)];
        return (unit, rarity, isRateUp);
    }

    private int RollRarity()
    {
        var roll = _rng.NextDouble();
        var cumulative = 0.0;

        foreach ( var (rarity, rate) in BaseRarityRates.OrderBy(x => x.Key) )
        {
            cumulative += rate;
            if ( roll < cumulative )
                return rarity;
        }

        return 3; // Fallback
    }
}

// Result helper
public readonly record struct Result<T>
{
    public T? Value { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value)
    {
        return new Result<T> { Value = value };
    }

    public static Result<T> Failure(string error)
    {
        return new Result<T> { Error = error };
    }
}