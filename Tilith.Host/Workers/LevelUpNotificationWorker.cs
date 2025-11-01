// Tilith.Host/Workers/LevelUpNotificationWorker.cs

using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Tilith.Bot;
using Tilith.Core.Data;
using Tilith.Core.Models;
using Tilith.Core.Services;

namespace Tilith.Host.Workers;

public sealed class LevelUpNotificationWorker : BackgroundService
{
    private readonly TilithBot _bot;
    private readonly ElementColorService _elementColorService;
    private readonly LevelUpNotificationService _levelUpNotificationService;
    private readonly ILogger<LevelUpNotificationWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UnitService _unitService;

    public LevelUpNotificationWorker(LevelUpNotificationService levelUpNotificationService,
        TilithBot bot,
        IServiceScopeFactory scopeFactory,
        UnitService unitService,
        ILogger<LevelUpNotificationWorker> logger,
        ElementColorService elementColorService)
    {
        _levelUpNotificationService = levelUpNotificationService;
        _bot = bot;
        _scopeFactory = scopeFactory;
        _unitService = unitService;
        _logger = logger;
        _elementColorService = elementColorService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LevelUpNotificationWorker started (awaiting Discord ready...)");
        await _bot.WaitForReadyAsync(stoppingToken);

        await foreach ( var notification in _levelUpNotificationService.Reader.ReadAllAsync(stoppingToken) )
        {
            try
            {
                if ( _bot.Client.GetChannel(notification.ChannelId) is not ISocketMessageChannel channel )
                {
                    _logger.LogWarning("Channel {ChannelId} not found for user {UserId}", notification.ChannelId, notification.UserId);
                    continue;
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<TilithDbContext>();
                var user = await context.Users
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(u => u.DiscordId == notification.UserId, stoppingToken);

                if ( user is null )
                {
                    _logger.LogWarning("User {UserId} not found in database", notification.UserId);
                    continue;
                }

                string? wallpaperUrl = null;
                if ( user.FavoriteUnitId is not null )
                {
                    var unit = _unitService.Units.FirstOrDefault(u => u.UnitId == user.FavoriteUnitId);
                    wallpaperUrl = unit.ImageUrl;
                }

                var guildUser = _bot.Client.GetGuild(notification.GuildId).GetUser(notification.UserId);
                var displayName = guildUser?.DisplayName ?? "Unknown";
                var avatarUrl = guildUser?.GetDisplayAvatarUrl(ImageFormat.Png);
                var elementColor = _elementColorService.GetImageSharpColor(guildUser?.Roles.Select(role => role.Id) ?? null);

                var rank = await context.Users.CountAsync(u => u.Experience > notification.Experience, stoppingToken) + 1;
                var (level, currentXp, nextXp) = LevelCalculator.GetLevelProgress(notification.Experience);
                var progressInLevel = notification.Experience - currentXp;

                var bannerBytes = await LevelUpBannerGenerator.GenerateAsync(
                    displayName,
                    avatarUrl,
                    rank,
                    level,
                    progressInLevel,
                    nextXp - currentXp,
                    wallpaperUrl,
                    elementColor,
                    stoppingToken
                );

                using var stream = new MemoryStream(bannerBytes);
                var file = new FileAttachment(stream, "levelup.png");

                await channel.SendFileAsync(file, $"🎉 <@{notification.UserId}> leveled up to **Level {level}**!");
            }
            catch ( Exception ex )
            {
                _logger.LogError(ex, "Failed to send level-up banner for user {UserId}", notification.UserId);
            }
        }
    }
}