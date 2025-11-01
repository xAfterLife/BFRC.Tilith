using Discord;
using Discord.Interactions;
using Tilith.Core.Services;

namespace Tilith.Bot.Modules;

[Group("banner", "Banner management commands")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
[RequireUserPermission(GuildPermission.Administrator)]
public sealed class BannerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BannerService _bannerService;
    private readonly UnitService _unitService;

    public BannerModule(BannerService bannerService, UnitService unitService)
    {
        _bannerService = bannerService;
        _unitService = unitService;
    }

    [SlashCommand("list", "List all banners")]
    [RequireUserPermission(GuildPermission.SendMessages)]
    public async Task ListBannersAsync([Summary("active-only", "Show only active banners")] bool activeOnly = true)
    {
        await DeferAsync(true);

        var banners = await _bannerService.GetActiveBannersAsync();

        if ( banners.Count == 0 )
        {
            await FollowupAsync("No banners found.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
                    .WithTitle("📢 Banners")
                    .WithColor(Color.Gold)
                    .WithDescription(string.Join("\n", banners.Select(b =>
                                $"**{b.Id}**: {b.Name} {(b.IsActive ? "🟢" : "🔴")}\n" +
                                $"  └ {b.BannerUnits.Count} units | {b.StartDateUtc:yyyy-MM-dd} → {b.EndDateUtc?.ToString("yyyy-MM-dd") ?? "∞"}"
                            )
                        )
                    )
                    .WithFooter($"Total: {banners.Count}")
                    .WithCurrentTimestamp()
                    .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("create", "Create a new banner")]
    public async Task CreateBannerAsync([Summary("name", "Banner name")] string name,
        [Summary("units", "Comma-separated unit IDs")]
        string unitIds,
        [Summary("description", "Banner description")]
        string? description = null,
        [Summary("start", "Start date (yyyy-MM-dd)")]
        string? startDate = null,
        [Summary("end", "End date (yyyy-MM-dd)")]
        string? endDate = null,
        [Summary("active", "Is active")] bool isActive = true,
        [Summary("image", "Image URL")] string? imageUrl = null)
    {
        await DeferAsync(true);

        var unitArray = unitIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
        if ( unitArray.Length == 0 )
        {
            await FollowupAsync("❌ At least one unit ID is required.", ephemeral: true);
            return;
        }

        var start = string.IsNullOrWhiteSpace(startDate)
            ? DateTime.UtcNow
            : DateTime.Parse(startDate).ToUniversalTime();

        var end = string.IsNullOrWhiteSpace(endDate)
            ? (DateTime?)null
            : DateTime.Parse(endDate).ToUniversalTime();

        try
        {
            var banner = await _bannerService.CreateBannerAsync(name, description, start, end, isActive, imageUrl, unitArray, CancellationToken.None);
            await FollowupAsync($"✅ Created banner **{banner.Id}: {banner.Name}** with {unitArray.Length} units.", ephemeral: true);
        }
        catch ( InvalidOperationException ex )
        {
            await FollowupAsync($"❌ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("activate", "Activate a banner")]
    public async Task ActivateBannerAsync([Summary("id", "Banner ID")] int id)
    {
        await DeferAsync(true);

        var updated = await _bannerService.UpdateBannerAsync(
            id,
            null, null, null, null,
            true,
            null, null,
            CancellationToken.None
        );

        if ( !updated )
        {
            await FollowupAsync($"❌ Banner {id} not found.", ephemeral: true);
            return;
        }

        await FollowupAsync($"✅ Activated banner **{id}**.", ephemeral: true);
    }

    [SlashCommand("deactivate", "Deactivate a banner")]
    public async Task DeactivateBannerAsync([Summary("id", "Banner ID")] int id)
    {
        await DeferAsync(true);

        var updated = await _bannerService.UpdateBannerAsync(
            id,
            null, null, null, null,
            false,
            null, null,
            CancellationToken.None
        );

        if ( !updated )
        {
            await FollowupAsync($"❌ Banner {id} not found.", ephemeral: true);
            return;
        }

        await FollowupAsync($"✅ Deactivated banner **{id}**.", ephemeral: true);
    }

    [SlashCommand("delete", "Delete a banner")]
    public async Task DeleteBannerAsync([Summary("id", "Banner ID")] int id)
    {
        await DeferAsync(true);

        var deleted = await _bannerService.DeleteBannerAsync(id, CancellationToken.None);

        if ( !deleted )
        {
            await FollowupAsync($"❌ Banner {id} not found.", ephemeral: true);
            return;
        }

        await FollowupAsync($"✅ Deleted banner **{id}**.", ephemeral: true);
    }

    [SlashCommand("info", "View banner details")]
    public async Task InfoBannerAsync([Summary("id", "Banner ID")] int id)
    {
        await DeferAsync(true);

        var banner = await _bannerService.GetBannerByIdAsync(id);

        if ( banner is null )
        {
            await FollowupAsync($"❌ Banner {id} not found.", ephemeral: true);
            return;
        }

        var units = banner.BannerUnits
                          .Select(bu => _unitService.Units.FirstOrDefault(u => u.UnitId == bu.UnitId))
                          .Where(u => u != default)
                          .Select(u => $"• {u.Name} ({u.Rarity}★)")
                          .ToList();

        var embed = new EmbedBuilder()
                    .WithTitle($"📢 {banner.Name}")
                    .WithDescription(banner.Description ?? "No description")
                    .WithColor(banner.IsActive ? Color.Green : Color.Red)
                    .AddField("Status", banner.IsActive ? "🟢 Active" : "🔴 Inactive", true)
                    .AddField("Start", banner.StartDateUtc.ToString("yyyy-MM-dd HH:mm"), true)
                    .AddField("End", banner.EndDateUtc?.ToString("yyyy-MM-dd HH:mm") ?? "No end date", true)
                    .AddField($"Units ({units.Count})", string.Join("\n", units.Take(10)))
                    .WithFooter($"Banner ID: {banner.Id}")
                    .WithCurrentTimestamp();

        if ( !string.IsNullOrWhiteSpace(banner.ImageUrl) )
            embed.WithImageUrl(banner.ImageUrl);

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }
}