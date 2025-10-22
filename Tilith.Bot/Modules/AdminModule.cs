using Discord;
using Discord.Interactions;
using Tilith.Core.Data;
using Tilith.Core.Entities;
using Tilith.Core.Models;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

[Group("admin", "Admin commands")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
[RequireUserPermission(GuildPermission.Administrator)]
public sealed class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TilithDbContext _context;
    private readonly LevelCacheService _levelCache;

    public AdminModule(TilithDbContext context, LevelCacheService levelCache)
    {
        _context = context;
        _levelCache = levelCache;
    }

    [SlashCommand("give-gems", "Give gems to a user")]
    public async Task GiveGemsAsync([Summary("user", "User to give gems to")] IUser user,
        [Summary("amount", "Amount of gems")] [MinValue(1)]
        int amount)
    {
        await DeferAsync(true);

        var dbUser = await _context.Users.FindAsync(user.Id);
        if ( dbUser is null )
        {
            dbUser = new User { DiscordId = user.Id, Gems = amount };
            _context.Users.Add(dbUser);
        }
        else
        {
            dbUser.Gems += amount;
        }

        await _context.SaveChangesAsync();

        await FollowupAsync($"✅ Gave {amount} gems to {user.Mention}. They now have **{dbUser.Gems} gems**.");
    }

    [SlashCommand("set-xp", "Set a user's XP")]
    public async Task SetXpAsync([Summary("user", "User to modify")] IUser user,
        [Summary("xp", "New XP amount")] [MinValue(0)]
        long xp)
    {
        await DeferAsync(true);

        var dbUser = await _context.Users.FindAsync(user.Id);
        var oldLevel = dbUser != null ? LevelCalculator.CalculateLevel(dbUser.Experience) : 1;

        if ( dbUser is null )
        {
            dbUser = new User { DiscordId = user.Id, Experience = xp };
            _context.Users.Add(dbUser);
        }
        else
        {
            dbUser.Experience = xp;
        }

        var newLevel = LevelCalculator.CalculateLevel(xp);
        await _context.SaveChangesAsync();

        // Invalidate cache after manual XP modification
        _levelCache.InvalidateUser(user.Id);

        await FollowupAsync($"✅ Set {user.Mention}'s XP to **{xp:N0}** (Level {oldLevel} → {newLevel}). Cache invalidated.");
    }
}