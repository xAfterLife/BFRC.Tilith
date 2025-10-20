using Tilith.Bot;

namespace Tilith.Host.Workers;

public sealed class DiscordBotWorker : IHostedService
{
    private readonly TilithBot _bot;
    private readonly ILogger<DiscordBotWorker> _logger;

    public DiscordBotWorker(TilithBot bot, ILogger<DiscordBotWorker> logger)

    {
        _bot = bot;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await _bot.StartAsync(ct);

            // Wait for Ready + command registration before marking healthy
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
            await _bot.WaitForReadyAsync(linked.Token);

            _logger.LogInformation("Discord bot fully initialized and ready");
        }
        catch ( Exception ex )
        {
            _logger.LogCritical(ex, "Failed to start Discord bot");
            throw; // Crash the host to surface the issue
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        return _bot.StopAsync();
    }
}