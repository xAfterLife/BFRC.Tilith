using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Tilith.Core.Data;
using Tilith.Core.Models;

namespace Tilith.Bot.Modules;

public sealed class XpModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TilithDbContext _context;

    public XpModule(TilithDbContext context)
    {
        _context = context;
    }

    [SlashCommand("level", "View your current level and XP")]
    public async Task LevelAsync([Summary("user", "View another user's level (optional)")] IUser? targetUser = null)
    {
        await DeferAsync();

        var user = targetUser ?? Context.User;
        var dbUser = await _context.Users.FindAsync(user.Id);

        if ( dbUser is null )
        {
            await FollowupAsync(
                $"{user.Mention} hasn't earned any XP yet! Send messages to level up.",
                ephemeral: targetUser is null
            );
            return;
        }

        var (level, currentXp, nextXp) = LevelCalculator.GetLevelProgress(dbUser.Experience);
        var progressInLevel = dbUser.Experience - currentXp;
        var xpNeeded = nextXp - currentXp;
        var percentage = (double)progressInLevel / xpNeeded * 100;

        // Calculate rank
        var rank = await _context.Users
                                 .CountAsync(u => u.Experience > dbUser.Experience) +
                   1;

        var progressBar = CreateProgressBar(percentage, 20);

        var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle($"{user.Username}'s Level")
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .AddField("Level", level, true)
                    .AddField("Rank", $"#{rank}", true)
                    .AddField("Total XP", dbUser.Experience.ToString("N0"), true)
                    .AddField("Progress to Next Level",
                        $"{progressBar}\n{progressInLevel:N0} / {xpNeeded:N0} ({percentage:F1}%)"
                    )
                    .AddField("Gems", $"💎 {dbUser.Gems}", true)
                    .WithFooter($"Created: {dbUser.CreatedAtUtc:yyyy-MM-dd}")
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("leaderboard", "View the XP leaderboard")]
    public async Task LeaderboardAsync([Summary("type", "Leaderboard type")] [Choice("XP", "xp")] [Choice("Gems", "gems")] string type = "xp",
        [Summary("limit", "Number of users to display (1-25)")] [MinValue(1)] [MaxValue(25)]
        int limit = 10)
    {
        await DeferAsync();

        if ( type == "gems" )
        {
            await ShowGemsLeaderboardAsync(limit);
        }
        else
        {
            await ShowXpLeaderboardAsync(limit);
        }
    }

    private async Task ShowXpLeaderboardAsync(int limit)
    {
        var topUsers = await _context.Users
                                     .OrderByDescending(u => u.Experience)
                                     .Take(limit)
                                     .Select(u => new { u.DiscordId, u.Experience })
                                     .ToListAsync();

        if ( topUsers.Count == 0 )
        {
            await FollowupAsync("📊 No users have earned XP yet!");
            return;
        }

        var description = string.Join("\n", topUsers.Select((u, index) =>
                {
                    var level = LevelCalculator.CalculateLevel(u.Experience);
                    var medal = index switch
                    {
                        0 => "🥇",
                        1 => "🥈",
                        2 => "🥉",
                        _ => $"`#{index + 1:D2}`"
                    };
                    return $"{medal} <@{u.DiscordId}> - **Level {level}** ({u.Experience:N0} XP)";
                }
            )
        );

        // Add user's own rank if not in top
        var userRank = await _context.Users
                                     .CountAsync(u => u.Experience >
                                                      _context.Users.Where(x => x.DiscordId == Context.User.Id)
                                                              .Select(x => x.Experience)
                                                              .FirstOrDefault()
                                     ) +
                       1;

        var footer = userRank <= limit
            ? $"Showing top {topUsers.Count} users"
            : $"Showing top {topUsers.Count} users • You are rank #{userRank}";

        var embed = new EmbedBuilder()
                    .WithColor(Color.Gold)
                    .WithTitle("📊 XP Leaderboard")
                    .WithDescription(description)
                    .WithFooter(footer)
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }

    private async Task ShowGemsLeaderboardAsync(int limit)
    {
        var topUsers = await _context.Users
                                     .Where(u => u.Gems > 0)
                                     .OrderByDescending(u => u.Gems)
                                     .Take(limit)
                                     .Select(u => new { u.DiscordId, u.Gems })
                                     .ToListAsync();

        if ( topUsers.Count == 0 )
        {
            await FollowupAsync("💎 No users have collected gems yet!");
            return;
        }

        var description = string.Join("\n", topUsers.Select((u, index) =>
                {
                    var medal = index switch
                    {
                        0 => "🥇",
                        1 => "🥈",
                        2 => "🥉",
                        _ => $"`#{index + 1:D2}`"
                    };
                    return $"{medal} <@{u.DiscordId}> - **{u.Gems:N0}** 💎";
                }
            )
        );

        var embed = new EmbedBuilder()
                    .WithColor(Color.Purple)
                    .WithTitle("💎 Gems Leaderboard")
                    .WithDescription(description)
                    .WithFooter($"Showing top {topUsers.Count} gem collectors")
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }

    private static string CreateProgressBar(double percentage, int length)
    {
        var filled = (int)(percentage / 100 * length);
        var empty = length - filled;

        return $"[{'█'.ToString().PadRight(filled, '█')}{'░'.ToString().PadRight(empty, '░')}]";
    }
}