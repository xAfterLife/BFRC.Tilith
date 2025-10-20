using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Tilith.Core.Services;

namespace Tilith.Bot.Autocomplete;

public sealed class UnitAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var unitService = services.GetRequiredService<UnitService>();
        var input = autocompleteInteraction.Data.Current.Value.ToString() ?? string.Empty;

        var suggestions = unitService.Units
                                     .Where(u => u.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
                                     .OrderBy(u => u.Name)
                                     .Take(5) // Discord limits to 25 suggestions
                                     .Select(u => new AutocompleteResult(u.Name, u.Name))
                                     .ToList();

        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
    }
}