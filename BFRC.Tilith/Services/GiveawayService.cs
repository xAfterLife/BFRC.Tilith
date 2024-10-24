using System.Collections.Concurrent;
using BFRC.Tilith.Models;
using Discord;
using Discord.WebSocket;

namespace BFRC.Tilith.Services;

public class GiveawayService
{
    private readonly ConcurrentDictionary<ulong, GiveawayInfo> _activeGiveaways;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly DiscordSocketClient _client;
    private readonly SemaphoreSlim _lock;

    public GiveawayService(DiscordSocketClient client)
    {
        _client = client;
        _activeGiveaways = new ConcurrentDictionary<ulong, GiveawayInfo>();
        _lock = new SemaphoreSlim(1, 1);
        _cancellationTokenSource = new CancellationTokenSource();

        // Start background task for winner selection
        _ = RunGiveawayCheckerAsync(_cancellationTokenSource.Token);
    }

    private async Task RunGiveawayCheckerAsync(CancellationToken cancellationToken)
    {
        while ( !cancellationToken.IsCancellationRequested )
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var giveawaysToProcess = _activeGiveaways.Values.Where(g => g.NextDrawTime <= now).ToList();

                foreach ( var giveaway in giveawaysToProcess )
                    await ProcessGiveawayDrawAsync(giveaway);
            }
            finally
            {
                _lock.Release();
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private async Task ProcessGiveawayDrawAsync(GiveawayInfo giveaway)
    {
        if ( await _client.GetGuild(giveaway.GuildId)
                          ?.GetTextChannel(giveaway.ChannelId)
                          ?.GetMessageAsync(giveaway.MessageId)! is not IUserMessage message )
            return;

        var reactions = await message.GetReactionUsersAsync(new Emoji("🎉"), 1000).FlattenAsync();
        var eligibleUsers = reactions.Where(u => !u.IsBot).ToList();

        if ( eligibleUsers.Any() )
        {
            var winner = eligibleUsers[Random.Shared.Next(eligibleUsers.Count)];
            await message.Channel.SendMessageAsync(
                $"Congratulations {winner.Mention}! You won: {giveaway.Prize}"
            );

            giveaway.Winners.Add(winner.Id);
        }

        // Update next draw time or end giveaway
        if ( giveaway.RemainingDraws > 1 )
        {
            giveaway.RemainingDraws--;
            giveaway.NextDrawTime = DateTimeOffset.UtcNow.Add(giveaway.DrawInterval);

            // Update the message with current status
            var embedBuilder = new EmbedBuilder()
                               .WithTitle("🎉 GIVEAWAY 🎉")
                               .WithDescription($"Prize: {giveaway.Prize}\nReact with 🎉 to enter!")
                               .WithColor(Color.Green)
                               .WithFooter(footer =>
                                   footer.Text = $"Ends {giveaway.EndTime:g} UTC • {giveaway.RemainingDraws} draws remaining"
                               )
                               .WithTimestamp(giveaway.EndTime);

            if ( !string.IsNullOrWhiteSpace(giveaway.GiveawayImageUrl) )
                embedBuilder.WithImageUrl(giveaway.GiveawayImageUrl);

            await message.ModifyAsync(msg => msg.Embed = embedBuilder.Build());
        }
        else
        {
            _activeGiveaways.TryRemove(giveaway.MessageId, out _);

            // Create ended giveaway embed
            var embedBuilder = new EmbedBuilder()
                               .WithTitle("🎉 GIVEAWAY ENDED 🎉")
                               .WithDescription($"Prize: {giveaway.Prize}\n\nWinners: {string.Join(", ", giveaway.Winners.Select(id => $"<@{id}>"))}")
                               .WithColor(Color.Red)
                               .WithFooter(footer => footer.Text = "Giveaway has ended")
                               .WithTimestamp(DateTimeOffset.UtcNow);

            // Use ended image if provided, otherwise keep the original image
            if ( !string.IsNullOrWhiteSpace(giveaway.EndedImageUrl) )
                embedBuilder.WithImageUrl(giveaway.EndedImageUrl);
            else if ( !string.IsNullOrWhiteSpace(giveaway.GiveawayImageUrl) )
                embedBuilder.WithImageUrl(giveaway.GiveawayImageUrl);

            await message.ModifyAsync(msg => msg.Embed = embedBuilder.Build());
        }
    }

    public async Task<bool> AddGiveaway(GiveawayInfo giveaway)
    {
        if ( giveaway == null )
            throw new ArgumentNullException(nameof(giveaway));

        // Validate giveaway parameters
        if ( giveaway.EndTime <= DateTimeOffset.UtcNow )
            throw new ArgumentException("Giveaway end time must be in the future.");

        if ( giveaway.EndTime > DateTimeOffset.UtcNow.AddDays(30) )
            throw new ArgumentException("Giveaway duration cannot exceed 30 days.");

        if ( giveaway.RemainingDraws <= 0 )
            throw new ArgumentException("Remaining draws must be greater than 0.");

        if ( string.IsNullOrWhiteSpace(giveaway.Prize) )
            throw new ArgumentException("Prize cannot be empty.");

        // Validate draw interval
        if ( giveaway.DrawInterval <= TimeSpan.Zero )
            throw new ArgumentException("Draw interval must be greater than zero.");

        // Validate image URLs if provided
        if ( !string.IsNullOrWhiteSpace(giveaway.GiveawayImageUrl) &&
             !Uri.TryCreate(giveaway.GiveawayImageUrl, UriKind.Absolute, out _) )
            throw new ArgumentException("Invalid giveaway image URL.");

        if ( !string.IsNullOrWhiteSpace(giveaway.EndedImageUrl) &&
             !Uri.TryCreate(giveaway.EndedImageUrl, UriKind.Absolute, out _) )
            throw new ArgumentException("Invalid ended image URL.");

        await _lock.WaitAsync();
        try
        {
            return !_activeGiveaways.ContainsKey(giveaway.MessageId) && _activeGiveaways.TryAdd(giveaway.MessageId, giveaway);
        }
        finally
        {
            _lock.Release();
        }
    }
}