using BFRC.Tilith.Enums;
using BFRC.Tilith.Models;
using BFRC.Tilith.Services;
using Discord;
using Discord.Interactions;

namespace BFRC.Tilith.Modules;

[Group("giveaway", "Manage giveaways")]
public class GiveawayModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly GiveawayService _giveawayService;

    public GiveawayModule(GiveawayService giveawayService)
    {
        _giveawayService = giveawayService;
    }

    [SlashCommand("start", "Start a new giveaway")]
    public async Task StartGiveawayAsync(
        [Summary("prize", "What to give away")] string prize,
        [Summary("duration", "Duration value")] int duration,
        [Summary("timeunit", "Time unit for duration")] TimeUnit timeUnit,
        [Summary("winners", "Number of winners")] int winners = 1,
        [Summary("image", "URL for giveaway image")] string? imageUrl = null,
        [Summary("endimage", "URL for ended giveaway image")] string? endImageUrl = null)
    {
        await DeferAsync();

        try
        {
            var totalDuration = timeUnit switch
            {
                TimeUnit.Minutes => TimeSpan.FromMinutes(duration),
                TimeUnit.Hours => TimeSpan.FromHours(duration),
                TimeUnit.Days => TimeSpan.FromDays(duration),
                _ => throw new ArgumentException("Invalid time unit specified")
            };

            // Validate duration
            if ( totalDuration <= TimeSpan.Zero || totalDuration > TimeSpan.FromDays(30) )
            {
                await ModifyOriginalResponseAsync(msg =>
                    msg.Content = "Duration must be greater than 0 and not exceed 30 days."
                );
                return;
            }

            // Calculate draw interval based on duration and winners
            var drawInterval = CalculateDrawInterval(totalDuration, winners);

            var durationText = FormatDuration(duration, timeUnit);

            var embedBuilder = new EmbedBuilder()
                               .WithTitle("🎉 GIVEAWAY 🎉")
                               .WithDescription($"Prize: {prize}\nReact with 🎉 to enter!")
                               .WithColor(Color.Green)
                               .WithFooter(footer => footer.Text = $"Ends in {durationText} • {winners} winner(s)")
                               .WithCurrentTimestamp();

            // Add image if provided and valid
            if ( !string.IsNullOrWhiteSpace(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out _) )
                embedBuilder.WithImageUrl(imageUrl);

            var embed = embedBuilder.Build();
            var message = await Context.Channel.SendMessageAsync(embed: embed);
            await message.AddReactionAsync(new Emoji("🎉"));

            var giveaway = new GiveawayInfo
            {
                MessageId = message.Id,
                ChannelId = Context.Channel.Id,
                GuildId = Context.Guild.Id,
                Prize = prize,
                GiveawayImageUrl = imageUrl,
                EndedImageUrl = endImageUrl,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.Add(totalDuration),
                NextDrawTime = CalculateNextDrawTime(totalDuration, winners),
                DrawInterval = drawInterval,
                RemainingDraws = winners
            };

            await _giveawayService.AddGiveaway(giveaway);
            await ModifyOriginalResponseAsync(msg =>
                msg.Content = $"Giveaway started successfully! Duration: {durationText}"
            );
        }
        catch ( Exception )
        {
            await ModifyOriginalResponseAsync(msg =>
                msg.Content = "An error occurred while starting the giveaway."
            );
        }
    }

    private static TimeSpan CalculateDrawInterval(TimeSpan totalDuration, int winners)
    {
        return winners <= 1 ? totalDuration : TimeSpan.FromTicks(totalDuration.Ticks / winners);
    }

    private static DateTimeOffset CalculateNextDrawTime(TimeSpan totalDuration, int winners)
    {
        return DateTimeOffset.UtcNow.Add(winners <= 1 ? totalDuration : CalculateDrawInterval(totalDuration, winners));
    }

    private static string FormatDuration(int duration, TimeUnit unit)
    {
        return (duration, unit) switch
        {
            (1, TimeUnit.Days) => "1 day",
            (1, TimeUnit.Hours) => "1 hour",
            (1, TimeUnit.Minutes) => "1 minute",
            (_, TimeUnit.Days) => $"{duration} days",
            (_, TimeUnit.Hours) => $"{duration} hours",
            (_, TimeUnit.Minutes) => $"{duration} minutes",
            _ => throw new ArgumentException("Invalid time unit")
        };
    }
}