using Discord.Interactions;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

public sealed class GemModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly GemService _gemService;

    public GemModule(GemService gemService)
    {
        _gemService = gemService;
    }

    [SlashCommand("daily", "Claim your daily gems")]
    public async Task DailyAsync()
    {
        // Defer to show "Bot is thinking..." (commands must respond within 3s)
        await DeferAsync();

        var result = await _gemService.ClaimDailyGemsAsync(Context.User.Id);

        if ( result.Success )
        {
            await FollowupAsync($"✅ Claimed **5 gems**! You now have **{result.NewGemCount} gems**.");
        }
        else
        {
            var timeLeft = result.TimeUntilNext!.Value;
            await FollowupAsync(
                $"⏰ Daily already claimed! Next claim in **{timeLeft.Hours}h {timeLeft.Minutes}m**.",
                ephemeral: true // Only visible to user
            );
        }
    }
}