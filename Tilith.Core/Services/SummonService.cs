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

    // Base rates: 3★=50%, 4★=35%, 5★=12%, 6★=3%
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

    public SummonService(IServiceScopeFactory scopeFactory, UnitService unitService, ILogger<SummonService> logger)
    {
        _scopeFactory = scopeFactory;
        _unitService = unitService;
        _logger = logger;
    }

    public async ValueTask<Result<SummonResult>> SummonAsync(ulong discordId, int? bannerId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var user = await db.Users.FindAsync([discordId], ct);
                    if ( user is null || user.Gems < BaseSummonCost )
                        return Result<SummonResult>.Failure("Insufficient gems (need 5)");

                    Banner? banner = null;
                    if ( bannerId.HasValue )
                    {
                        banner = await db.Banners
                                         .AsNoTracking()
                                         .Include(b => b.BannerUnits)
                                         .FirstOrDefaultAsync(b => b.Id == bannerId && b.IsActive, ct);

                        if ( banner is null || banner.BannerUnits.Count == 0 )
                            return Result<SummonResult>.Failure("Banner not found or has no units");
                    }
                    else
                    {
                        return Result<SummonResult>.Failure("No banner specified—banner summon required");
                    }

                    var (unit, rarity, isRateUp) = RollUnit(banner);

                    user.Gems -= BaseSummonCost;
                    user.UpdatedAtUtc = DateTime.UtcNow;

                    // Update inventory (always track quantity)
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

                    // Create UserUnitInstance only if first summon (for future favorite selection)
                    var unitInstance = await db.UserUnits
                                               .FirstOrDefaultAsync(u => u.DiscordId == discordId && u.UnitId == unit.UnitId, ct);
                    if ( unitInstance is null )
                    {
                        db.UserUnits.Add(new UserUnitInstance
                            {
                                DiscordId = discordId,
                                UnitId = unit.UnitId,
                                UnitXp = 0,
                                IsFavorite = false,
                                CreatedAtUtc = DateTime.UtcNow,
                                UpdatedAtUtc = DateTime.UtcNow
                            }
                        );
                    }
                    // Note: Duplicate summons only increment UserInventory.Quantity

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

                    _logger.LogInformation("User {UserId} summoned {UnitId} (★{Rarity}) from banner {BannerId}",
                        discordId, unit.UnitId, rarity, bannerId
                    );

                    return Result<SummonResult>.Success(new SummonResult(unit, rarity, isRateUp, BaseSummonCost, user.Gems));
                }
                catch ( Exception ex )
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogError(ex, "Summon failed for user {UserId}", discordId);
                    throw;
                }
            }
        );
    }

    private (UnitData Unit, int Rarity, bool IsRateUp) RollUnit(Banner banner)
    {
        var rarity = RollRarity();

        var bannerUnitIds = banner.BannerUnits.Select(bu => bu.UnitId).ToHashSet();
        var pool = _unitService.Units
                               .Where(u => bannerUnitIds.Contains(u.UnitId) && u.Rarity == rarity)
                               .ToArray();

        if ( pool.Length == 0 )
        {
            rarity -= 1;
            pool = _unitService.Units
                               .Where(u => bannerUnitIds.Contains(u.UnitId) && u.Rarity == rarity)
                               .ToArray();
        }

        if ( pool.Length == 0 )
        {
            throw new InvalidOperationException($"Banner {banner.Id} has no valid units");
        }

        var unit = pool[_rng.Next(pool.Length)];

        return (unit, rarity, false);
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

        return 3; // fallback
    }
}

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