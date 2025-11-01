using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tilith.Bot.Autocomplete;
using Tilith.Core.Data;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

public sealed class FavoriteUnitModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TilithDbContext _context;
    private readonly UnitService _unitService;

    public FavoriteUnitModule(TilithDbContext context, UnitService unitService)
    {
        _context = context;
        _unitService = unitService;
    }

    [SlashCommand("unit", "Displays information about a Brave Frontier unit")]
    public async Task UnitAsync([Summary("name", "The name of the unit")] [Autocomplete(typeof(UnitAutocompleteHandler))] string name)
    {
        await DeferAsync();

        var unit = _unitService.GetByName(name);
        if ( unit is null )
        {
            await FollowupAsync("Unit not found. Please try a different name.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
                    .WithTitle(unit.Value.Name)
                    .WithDescription($"Rarity: {unit.Value.Rarity}")
                    .WithImageUrl(unit.Value.ImageUrl)
                    .WithColor(Color.Blue) // Optional: Customize color
                    .AddField("Unit ID", unit.Value.UnitId, true)
                    .AddField("Data ID", unit.Value.UnitDataId, true)
                    .WithFooter("")
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("set-favorite", "Set your favorite unit (it will level alongside you)")]
    public async Task SetFavoriteAsync([Summary("unit", "Unit name")] [Autocomplete(typeof(UserUnitsAutocompleteHandler))] string unitName)
    {
        await DeferAsync(true);

        var unit = _unitService.GetByName(unitName);
        if ( unit is null )
        {
            await FollowupAsync("❌ Unit not found.", ephemeral: true);
            return;
        }

        var userUnit = await _context.UserUnits
                                     .FirstOrDefaultAsync(u => u.DiscordId == Context.User.Id && u.UnitId == unit.Value.UnitId);

        if ( userUnit is null )
        {
            await FollowupAsync("❌ You don't own this unit. Summon it first!", ephemeral: true);
            return;
        }

        // Clear previous favorite
        var oldFavorite = await _context.UserUnits
                                        .FirstOrDefaultAsync(u => u.DiscordId == Context.User.Id && u.IsFavorite);
        if ( oldFavorite is not null )
            oldFavorite.IsFavorite = false;

        userUnit.IsFavorite = true;

        var user = await _context.Users.FindAsync(Context.User.Id);
        if ( user is not null )
            user.FavoriteUnitId = unit.Value.UnitId;

        await _context.SaveChangesAsync();

        await FollowupAsync($"✅ Set **{unit.Value.Name}** as your favorite unit!", ephemeral: true);
    }

    [SlashCommand("favorite", "View your favorite unit")]
    public async Task FavoriteAsync()
    {
        await DeferAsync();

        var user = await _context.Users.FindAsync(Context.User.Id);
        if ( user?.FavoriteUnitId is null )
        {
            await FollowupAsync("❌ You haven't set a favorite unit yet. Use `/set-favorite` to choose one!", ephemeral: true);
            return;
        }

        var userUnit = await _context.UserUnits
                                     .FirstOrDefaultAsync(u => u.DiscordId == Context.User.Id && u.UnitId == user.FavoriteUnitId);

        if ( userUnit is null )
        {
            await FollowupAsync("❌ Favorite unit data not found.", ephemeral: true);
            return;
        }

        var unit = _unitService.Units.FirstOrDefault(u => u.UnitId == userUnit.UnitId);
        if ( unit == default )
        {
            await FollowupAsync("❌ Unit data not found.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
                    .WithTitle($"⭐ {unit.Name}")
                    .WithColor(Color.Gold)
                    .AddField("Unit XP", $"{userUnit.UnitXp:N0}", true)
                    .AddField("Rarity", $"{unit.Rarity}★", true)
                    .WithImageUrl(unit.ImageUrl)
                    .WithFooter($"Set on {userUnit.CreatedAtUtc:yyyy-MM-dd}")
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }
}

// Autocomplete handler for user's owned units
public sealed class UserUnitsAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocomplete,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var db = services.GetRequiredService<TilithDbContext>();
        var unitService = services.GetRequiredService<UnitService>();
        var input = autocomplete.Data.Current.Value.ToString() ?? string.Empty;

        var ownedUnitIds = await db.UserUnits
                                   .Where(u => u.DiscordId == context.User.Id)
                                   .Select(u => u.UnitId)
                                   .ToListAsync();

        var suggestions = unitService.Units
                                     .Where(u => ownedUnitIds.Contains(u.UnitId) &&
                                                 u.Name.Contains(input, StringComparison.OrdinalIgnoreCase)
                                     )
                                     .OrderBy(u => u.Name)
                                     .Take(25)
                                     .Select(u => new AutocompleteResult(u.Name, u.Name));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}