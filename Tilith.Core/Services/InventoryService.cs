// Tilith.Core/Services/InventoryService.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tilith.Core.Data;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class InventoryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UnitService _unitService;

    public InventoryService(IServiceScopeFactory scopeFactory, UnitService unitService)
    {
        _scopeFactory = scopeFactory;
        _unitService = unitService;
    }

    public async ValueTask<List<(UnitData Unit, int Quantity)>> GetInventoryAsync(ulong discordId,
        int? rarityFilter = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var query = db.UserInventory
                      .AsNoTracking()
                      .Where(i => i.DiscordId == discordId);

        var items = await query
                          .OrderByDescending(i => i.Quantity)
                          .ThenBy(i => i.UnitId)
                          .Skip(skip)
                          .Take(take)
                          .ToListAsync(ct);

        var result = new List<(UnitData, int)>(items.Count);
        foreach ( var item in items )
        {
            var unit = _unitService.Units.FirstOrDefault(u => u.UnitId == item.UnitId);
            if ( unit == default )
                continue;

            if ( rarityFilter.HasValue && unit.Rarity != rarityFilter.Value )
                continue;

            result.Add((unit, item.Quantity));
        }

        return result;
    }

    public async ValueTask<int> GetTotalUnitsAsync(ulong discordId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        return await db.UserInventory
                       .Where(i => i.DiscordId == discordId)
                       .SumAsync(i => i.Quantity, ct);
    }
}