using BFRC.Tilith.Services;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BFRC.Tilith;

internal static class Program
{
    private static DiscordSocketClient _client = null!;
    private static IServiceProvider _services = null!;
    private static IConfiguration _configuration = null!;

    private static async Task Main()
    {
        _configuration = new ConfigurationBuilder()
                         .AddEnvironmentVariables("DC_")
                         .AddJsonFile("appsettings.json", true)
                         .Build();

        _services = new ServiceCollection()
                    .AddSingleton(_configuration)
                    .AddSingleton<DiscordSocketClient>()
                    .AddSingleton<LoggingService>()
                    .AddSingleton<CommandService>()
                    .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                    .AddSingleton<InteractionHandler>()
                    .AddSingleton<GiveawayService>()
                    .BuildServiceProvider();

        _client = _services.GetRequiredService<DiscordSocketClient>();
        _client.Log += LoggingService.LogAsync;

        // Here we can initialize the service that will register and execute our commands
        await _services.GetRequiredService<InteractionHandler>()
                       .InitializeAsync();

        // Bot token can be provided from the Configuration object we set up earlier
        await _client.LoginAsync(TokenType.Bot, _configuration["token"]);
        await _client.StartAsync();

        // Never quit the program until manually forced to.
        await Task.Delay(Timeout.Infinite);
    }
}