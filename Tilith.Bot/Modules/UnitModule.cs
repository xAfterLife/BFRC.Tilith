using Discord;
using Discord.Interactions;
using Tilith.Bot.Autocomplete;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

public class UnitModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UnitService _unitService;

    public UnitModule(UnitService unitService)
    {
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
                    .WithFooter("Data from Brave Frontier Wiki")
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }
}