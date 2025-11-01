// Tilith.Bot/Modules/SummonModule.cs

using Discord;
using Discord.Interactions;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

public sealed class SummonModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BannerService _bannerService;
    private readonly InventoryService _inventoryService;
    private readonly SummonService _summonService;

    public SummonModule(SummonService summonService,
        InventoryService inventoryService,
        BannerService bannerService)
    {
        _summonService = summonService;
        _inventoryService = inventoryService;
        _bannerService = bannerService;
    }

    [SlashCommand("summon", "Summon a unit from the gate")]
    public async Task SummonAsync([Summary("banner", "Banner to summon from (optional)")] [Autocomplete(typeof(BannerAutocompleteHandler))] int? bannerId = null)
    {
        await DeferAsync();

        var result = await _summonService.SummonAsync(Context.User.Id, bannerId);

        if ( !result.IsSuccess )
        {
            await FollowupAsync($"❌ {result.Error}", ephemeral: true);
            return;
        }

        // Start door animation
        var animMsg = await FollowupAsync("🚪 **The Summoning Gate rumbles...**");
        await Task.Delay(1500);

        await animMsg.ModifyAsync(m => m.Content = "✨ **The gate cracks open!**");
        await Task.Delay(1500);

        await animMsg.ModifyAsync(m => m.Content = "💎 **A brilliant light emerges!**");
        await Task.Delay(1000);

        // Reveal unit
        var summon = result.Value;
        var rarityColor = summon.Rarity switch
        {
            6 => Color.Gold,
            5 => Color.Purple,
            4 => Color.Blue,
            _ => Color.LightGrey
        };

        var embed = new EmbedBuilder()
                    .WithTitle($"{summon.Rarity} {summon.Unit.Name}")
                    .WithDescription(summon.IsRateUp ? "🔥 **RATE UP!**" : null)
                    .WithColor(rarityColor)
                    .WithImageUrl(summon.Unit.ImageUrl)
                    .AddField("Rarity", $"{summon.Rarity}★", true)
                    .AddField("Cost", $"{summon.GemsSpent} 💎", true)
                    .AddField("Remaining Gems", $"{summon.RemainingGems} 💎", true)
                    .WithCurrentTimestamp()
                    .Build();

        await animMsg.ModifyAsync(m =>
            {
                m.Content = $"🎉 **{Context.User.Mention} summoned:**";
                m.Embed = embed;
            }
        );
    }

    [SlashCommand("inventory", "View your unit collection")]
    public async Task InventoryAsync([Summary("rarity", "Filter by rarity")] [MinValue(3)] [MaxValue(6)] int? rarity = null,
        [Summary("page", "Page number")] [MinValue(1)]
        int page = 1)
    {
        await DeferAsync();

        const int pageSize = 10;
        var skip = (page - 1) * pageSize;

        var units = await _inventoryService.GetInventoryAsync(
            Context.User.Id, rarity, skip, pageSize
        );

        if ( units.Count == 0 )
        {
            await FollowupAsync("📦 Your inventory is empty. Use `/summon` to collect units!", ephemeral: true);
            return;
        }

        var total = await _inventoryService.GetTotalUnitsAsync(Context.User.Id);

        var description = string.Join("\n", units.Select(u =>
                $"{u.Unit.Rarity} **{u.Unit.Name}** (x{u.Quantity})"
            )
        );

        var embed = new EmbedBuilder()
                    .WithTitle("📦 Your Inventory")
                    .WithDescription(description)
                    .WithColor(Color.Teal)
                    .WithFooter($"Page {page} • Total Units: {total}")
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed);
    }
}