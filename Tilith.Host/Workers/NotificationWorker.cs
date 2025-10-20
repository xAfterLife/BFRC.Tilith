using Discord.WebSocket;
using Tilith.Bot;
using Tilith.Core.Services;

namespace Tilith.Host.Workers;

public sealed class NotificationWorker : BackgroundService
{
    private readonly TilithBot _bot;
    private readonly ILogger<NotificationWorker> _logger;
    private readonly NotificationService _notificationService;

    public NotificationWorker(NotificationService notificationService,
        TilithBot bot,
        ILogger<NotificationWorker> logger)
    {
        _notificationService = notificationService;
        _bot = bot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Worker started");

        // Wait for Discord client to be ready
        await _bot.WaitForReadyAsync(stoppingToken);

        await foreach ( var notification in _notificationService.Reader.ReadAllAsync(stoppingToken) )
        {
            try
            {
                var channel = _bot.Client.GetChannel(notification.ChannelId) as ISocketMessageChannel;
                if ( channel is null )
                {
                    _logger.LogWarning("Channel {ChannelId} not found for notification", notification.ChannelId);
                    continue;
                }

                var message = notification.NewLevel - notification.OldLevel == 1
                    ? $"🎉 <@{notification.UserId}> leveled up to **Level {notification.NewLevel}**!"
                    : $"🚀 <@{notification.UserId}> jumped to **Level {notification.NewLevel}**! (from {notification.OldLevel})";

                await channel.SendMessageAsync(message);
            }
            catch ( Exception ex )
            {
                _logger.LogError(ex, "Failed to send level-up notification for user {UserId}", notification.UserId);
            }
        }
    }
}