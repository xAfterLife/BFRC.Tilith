using Discord;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tilith.Bot;

namespace Tilith.Host.HealthChecks;

public sealed class DiscordHealthCheck : IHealthCheck
{
    private readonly TilithBot _bot;

    public DiscordHealthCheck(TilithBot bot)
    {
        _bot = bot;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var client = _bot.Client;
        var state = client.ConnectionState;

        return state switch
        {
            ConnectionState.Connected => Task.FromResult(
                HealthCheckResult.Healthy($"Connected (latency: {client.Latency}ms, guilds: {client.Guilds.Count})")
            ),

            ConnectionState.Connecting => Task.FromResult(
                HealthCheckResult.Degraded("Connecting to Discord gateway")
            ),

            ConnectionState.Disconnecting or ConnectionState.Disconnected => Task.FromResult(
                HealthCheckResult.Unhealthy($"Disconnected (state: {state})")
            ),

            _ => Task.FromResult(
                HealthCheckResult.Unhealthy($"Unknown state: {state}")
            )
        };
    }
}