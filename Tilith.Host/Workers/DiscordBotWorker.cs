using Tilith.Bot;

namespace Tilith.Host.Workers;

public sealed class DiscordBotWorker : IHostedService
{
    private readonly TilithBot _bot;

    public DiscordBotWorker(TilithBot bot)
    {
        _bot = bot;
    }

    public Task StartAsync(CancellationToken ct)
    {
        return _bot.StartAsync(ct);
    }

    public Task StopAsync(CancellationToken ct)
    {
        return _bot.StopAsync();
    }
}