// Tilith.Bot/Modules/XpModule.cs

using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Tilith.Core.Data;
using Tilith.Core.Models;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

public sealed class XpModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TilithDbContext _context;
    private readonly ElementColorService _elementColorService;
    private readonly UnitService _unitService;

    public XpModule(TilithDbContext context,
        ElementColorService elementColorService,
        UnitService unitService)
    {
        _context = context;
        _elementColorService = elementColorService;
        _unitService = unitService;
    }

    [SlashCommand("level", "View your current level and XP")]
    public async Task LevelAsync([Summary("user", "View another user's level (optional)")] IUser? targetUser = null)
    {
        await DeferAsync(targetUser is null);

        var user = targetUser ?? Context.User;
        var dbUser = await _context.Users
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(u => u.DiscordId == user.Id);

        if ( dbUser is null )
        {
            await FollowupAsync(
                $"{user.Mention} hasn't earned any XP yet! Send messages to level up.",
                ephemeral: true
            );
            return;
        }

        var (level, currentXp, nextXp) = LevelCalculator.GetLevelProgress(dbUser.Experience);
        var progressInLevel = dbUser.Experience - currentXp;
        var xpNeeded = nextXp - currentXp;
        var percentage = (double)progressInLevel / xpNeeded * 100;

        // Parallel rank queries
        var rank = await _context.Users.CountAsync(u => u.Experience > dbUser.Experience) + 1;

        // Fetch favorite unit
        string? favoriteUnitName = null;
        string? favoriteUnitImage = null;
        if ( dbUser.FavoriteUnitId is not null )
        {
            var favoriteUnit = await _context.UserUnits
                                             .AsNoTracking()
                                             .FirstOrDefaultAsync(u => u.DiscordId == dbUser.DiscordId && u.IsFavorite);

            if ( favoriteUnit is not null )
            {
                var unitData = _unitService.Units.FirstOrDefault(u => u.UnitId == favoriteUnit.UnitId);
                favoriteUnitName = unitData.Name;
                favoriteUnitImage = unitData.ImageUrl;
            }
        }

        // Get element color from user's roles (Discord.NET color for embeds)
        var embedColor = Context.Guild?.GetUser(user.Id) is { } guildUser
            ? _elementColorService.GetDiscordColor(guildUser.Roles.Select(r => r.Id))
            : new Color(169, 169, 169); // Gray fallback

        var embed = new EmbedBuilder()
                    .WithColor(embedColor) // ← Dynamic color based on element role
                    .WithAuthor(
                        $"{user.Username}'s Profile",
                        Context.Guild?.IconUrl
                    )
                    .AddField("**Level**", $"{level}", true)
                    .AddField("**Rank**", $"#{rank:N0}", true)
                    .AddField("**Total XP**", $"{dbUser.Experience:N0}", true)
                    .AddField(
                        "Progress to Next Level",
                        $"{CreateProgressBar(percentage, 24)}\n" +
                        $"**{progressInLevel:N0}** / {xpNeeded:N0} XP ({percentage:F1}%)"
                    )
                    .WithThumbnailUrl(user.GetDisplayAvatarUrl(ImageFormat.Png, 96))
                    .WithFooter($"Member since {dbUser.CreatedAtUtc:yyyy-MM-dd}")
                    .WithCurrentTimestamp();

        if ( favoriteUnitName is not null )
        {
            embed.AddField("Favorite Unit", $"⭐ {favoriteUnitName}", true);
        }

        if ( favoriteUnitImage is not null )
        {
            embed.WithImageUrl(favoriteUnitImage);
        }

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("leaderboard", "View the XP leaderboard")]
    public async Task LeaderboardAsync([Summary("limit", "Number of users to display (1-25)")] [MinValue(1)] [MaxValue(25)] int limit = 10)
    {
        await DeferAsync();
        await ShowXpLeaderboardAsync(limit);
    }

    private async Task ShowXpLeaderboardAsync(int limit)
    {
        var topUsers = await _context.Users
                                     .AsNoTracking()
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

        var userRank = await _context.Users.CountAsync(u => u.Experience >
                                                            _context.Users
                                                                    .Where(x => x.DiscordId == Context.User.Id)
                                                                    .Select(x => x.Experience)
                                                                    .FirstOrDefault()
                       ) +
                       1;

        var footer = userRank <= limit
            ? $"Showing top {topUsers.Count} users"
            : $"Showing top {topUsers.Count} users • You are rank #{userRank:N0}";

        var embed = new EmbedBuilder()
                    .WithColor(Color.Gold)
                    .WithTitle("📊 XP Leaderboard")
                    .WithDescription(description)
                    .WithFooter(footer)
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }

    private static string CreateProgressBar(double percentage, int length)
    {
        if ( length <= 0 )
            return string.Empty;
        percentage = Math.Clamp(percentage, 0.0, 100.0);
        var filled = (int)Math.Round(percentage / 100 * length);
        if ( filled > length )
            filled = length;

        return string.Create(length + 2, (filled, length), static (span, state) =>
            {
                span[0] = '[';
                var (filledCount, totalLength) = state;
                var i = 1;
                for ( ; i <= filledCount; i++ )
                    span[i] = '█';
                for ( ; i <= totalLength; i++ )
                    span[i] = '░';
                span[^1] = ']';
            }
        );
    }
}