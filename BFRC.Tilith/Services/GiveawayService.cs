using System.Collections.Concurrent;
using BFRC.Tilith.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace BFRC.Tilith.Services;

public class GiveawayService
{
    private static readonly Random Random = new();
    private readonly DiscordSocketClient _client;
    private readonly ConcurrentDictionary<ulong, Giveaway> _giveaways = new();

    public GiveawayService(IServiceProvider services)
    {
        _client = services.GetRequiredService<DiscordSocketClient>();
        _client.ReactionAdded += HandleReactionAddedAsync;
    }

    public async Task CreateGiveawayAsync(ITextChannel channel, string prize, TimeSpan duration, IUser host, string imageUrl = null)
    {
        var endTime = DateTime.UtcNow + duration;
        var giveaway = new Giveaway
        {
            ChannelId = channel.Id,
            Prize = prize,
            EndTime = endTime,
            HostId = host.Id
        };

        var timestampTag = new TimestampTag(endTime, TimestampTagStyles.Relative);
        var embed = new EmbedBuilder()
                    .WithTitle("Giveaway!")
                    .WithDescription($"Prize: {prize}\nEnds at: {timestampTag}\nReact with 🎉 to enter!")
                    .WithColor(Color.Gold)
                    .WithFooter($"Hosted by: {host.Username}")
                    .WithCurrentTimestamp();

        if ( !string.IsNullOrEmpty(imageUrl) )
            embed.WithImageUrl(imageUrl);

        var message = await channel.SendMessageAsync(embed: embed.Build());
        giveaway.MessageId = message.Id;

        _giveaways[message.Id] = giveaway;

        // Schedule the giveaway to end
        _ = Task.Delay(duration).ContinueWith(async _ => await EndGiveawayAsync(giveaway));
    }

    private Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
    {
        if ( reaction.Emote.Name != "🎉" )
            return Task.CompletedTask;

        if ( !_giveaways.TryGetValue(reaction.MessageId, out var giveaway) )
            return Task.CompletedTask;

        if ( giveaway.Participants.Contains(reaction.UserId) )
            return Task.CompletedTask;

        giveaway.Participants.Add(reaction.UserId);

        return Task.CompletedTask;
    }

    private async Task EndGiveawayAsync(Giveaway giveaway)
    {
        try
        {
            if ( !_giveaways.TryRemove(giveaway.MessageId, out _) )
                return;

            if ( _client.GetChannel(giveaway.ChannelId) is not ITextChannel channel )
                return;

            if ( await channel.GetMessageAsync(giveaway.MessageId) is not IUserMessage message )
                return;

            if ( giveaway.Participants.Count == 0 )
            {
                await message.ModifyAsync(x => x.Embed = new EmbedBuilder()
                                                         .WithTitle("Giveaway Ended")
                                                         .WithDescription("No one entered the giveaway.")
                                                         .WithColor(Color.Red)
                                                         .Build()
                );
                return;
            }

            var winner = giveaway.Participants[Random.Next(giveaway.Participants.Count)];
            await message.ModifyAsync(x => x.Embed = new EmbedBuilder()
                                                     .WithTitle("Giveaway Ended")
                                                     .WithDescription($"Winner: <@{winner}>!\nPrize: {giveaway.Prize}")
                                                     .WithColor(Color.Green)
                                                     .WithImageUrl("https://i.ytimg.com/vi/D3ZTUCoVrss/hqdefault.jpg")
                                                     .Build()
            );
        }
        catch ( Exception ex )
        {
            // Log the exception
            Console.WriteLine($"Error ending giveaway: {ex}");
        }
    }
}