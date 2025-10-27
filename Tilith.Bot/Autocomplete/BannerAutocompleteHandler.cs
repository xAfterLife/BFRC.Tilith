using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

public sealed class BannerAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocomplete,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var bannerService = services.GetRequiredService<BannerService>();
        var banners = await bannerService.GetActiveBannersAsync();

        var results = banners
                      .Take(25)
                      .Select(b => new AutocompleteResult(b.Name, b.Id));

        return AutocompletionResult.FromSuccess(results);
    }
}