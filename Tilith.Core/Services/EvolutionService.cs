using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tilith.Core.Data;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

/// <summary>
///     Handles unit evolution logic. Future: load chains from EvolutionChain table.
/// </summary>
public sealed class EvolutionService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Placeholder thresholds per stage                       20   50   100   250   500   1000   2500     amount of Messages
    // private static readonly long[] EvolutionThresholds = [200, 500, 1000, 2500, 5000, 10000, 25000];
    private static readonly long[] EvolutionThresholds = [1, 1, 1, 1, 1, 1, 1];
    private static IReadOnlyList<UnitEvolutionEntry> _evolutionEntries = null!;

    private readonly ILogger<EvolutionService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UnitService _unitService;

    public EvolutionService(IServiceScopeFactory scopeFactory, UnitService unitService, ILogger<EvolutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _unitService = unitService;
        _logger = logger;

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Tilith.Core.Resource.brave_frontier_evo_chains.json") ??
                           throw new InvalidOperationException("Embedded resource 'brave_frontier_evo_chains.json' not found.");

        var evolutionEntries = JsonSerializer.Deserialize<List<UnitEvolutionEntry>>(stream, Options) ??
                               throw new InvalidOperationException("Failed to deserialize brave_frontier_evo_chains.json.");

        _evolutionEntries = evolutionEntries.AsReadOnly();
    }

    /// <summary>
    ///     Check if the user's favorite unit can evolve; if so, increment stage.
    /// </summary>
    public async ValueTask<bool> TryEvolveAsync(ulong discordId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var user = await dbContext.Users.FindAsync([discordId], ct);
        if ( user?.FavoriteUnitId is null )
            return false;

        var userUnit = await dbContext.UserUnits
                                      .FirstOrDefaultAsync(u => u.DiscordId == discordId && u.UnitId == user.FavoriteUnitId, ct);
        if ( userUnit is null )
            return false;

        var unit = _unitService.Units.FirstOrDefault(u => u.UnitId == userUnit.UnitId);
        if ( string.IsNullOrWhiteSpace(unit.Name) )
            return false;

        var evoChain = _evolutionEntries.FirstOrDefault(x => x.UnitIds.Contains(unit.UnitId));
        if ( !evoChain.UnitIds.Contains(unit.UnitId) )
            return false;

        var stage = unit.Rarity - 1;
        if ( stage > EvolutionThresholds.Length )
            return false;

        var requiredXp = EvolutionThresholds[stage - 1];
        if ( userUnit.UnitXp < requiredXp )
            return false;

        userUnit.UnitXp = 0;
        userUnit.UnitId = evoChain.UnitIds[evoChain.UnitIds.IndexOf(userUnit.UnitId) + 1];
        user.FavoriteUnitId = userUnit.UnitId;
        userUnit.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} evolved unit {OldUnitId} → {NewUnitId} (Rarity {Rarity})",
            discordId, unit.UnitId, userUnit.UnitId, unit.Rarity
        );

        return true;
    }

    /// <summary>
    ///     Grant XP to user's favorite unit.
    /// </summary>
    public async ValueTask GrantUnitXpAsync(ulong discordId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var user = await dbContext.Users.FindAsync([discordId], ct);
        if ( user?.FavoriteUnitId is null )
            return;

        var userUnit = await dbContext.UserUnits
                                      .FirstOrDefaultAsync(u => u.DiscordId == discordId && u.UnitId == user.FavoriteUnitId, ct);
        if ( userUnit is null )
            return;

        userUnit.UnitXp += 10;
        userUnit.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
    }
}