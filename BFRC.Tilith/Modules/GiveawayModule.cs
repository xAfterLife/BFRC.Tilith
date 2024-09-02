using BFRC.Tilith.Services;
using Discord;
using Discord.Interactions;
using System.Threading.Tasks;

namespace BFRC.Tilith.Modules;

public class GiveawayModule : InteractionModuleBase<SocketInteractionContext>
{
    public required GiveawayService GiveawayService { get; set; }

    [SlashCommand("giveaway", "Start a giveaway")]
    public async Task StartGiveawayAsync(string prize, [Summary("Duration", "The Duration in Minutes")] double duration, [Summary("Image", "URL of the image to display")] string image = null)
    {
        await GiveawayService.CreateGiveawayAsync(Context.Channel as ITextChannel, prize, TimeSpan.FromMinutes(duration), Context.User, image);
        await RespondAsync("Done..", ephemeral: true);
    }
}