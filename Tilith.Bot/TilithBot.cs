using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tilith.Core.Services;

namespace Tilith.Bot;

public sealed class TilithBot
{
    private readonly InteractionService _interactions;
    private readonly ILogger<TilithBot> _logger;
    private readonly TaskCompletionSource _readyTcs = new();
    private readonly IServiceProvider _services;
    private readonly ulong? _testGuildId;
    private readonly string _token;
    private readonly XpService _xpService;

    public TilithBot(IConfiguration config,
        IServiceProvider services,
        ILogger<TilithBot> logger,
        XpService xpService)
    {
        _token = config["Discord:Token"] ?? throw new InvalidOperationException("Discord token missing");
        _testGuildId = config.GetValue<ulong?>("Discord:TestGuild");
        _services = services;
        _logger = logger;
        _xpService = xpService;

        Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
                LogLevel = LogSeverity.Info
            }
        );

        _interactions = new InteractionService(Client.Rest, new InteractionServiceConfig
            {
                DefaultRunMode = RunMode.Async,
                UseCompiledLambda = true // Performance: compiled expression trees
            }
        );

        Client.MessageReceived += OnMessageReceivedAsync;
        Client.Ready += OnReadyAsync;
        Client.InteractionCreated += OnInteractionCreatedAsync;
        Client.Log += LogAsync;
    }

    public DiscordSocketClient Client { get; }

    public async Task StartAsync(CancellationToken ct)
    {
        // Load interaction modules
        await _interactions.AddModulesAsync(typeof(TilithBot).Assembly, _services);

        await Client.LoginAsync(TokenType.Bot, _token);
        await Client.StartAsync();

        _logger.LogInformation("Discord bot started, waiting for ready event...");
    }

    public async Task StopAsync()
    {
        await Client.StopAsync();
        await Client.LogoutAsync();
    }

    public Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        return _readyTcs.Task.WaitAsync(cancellationToken);
    }

    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Discord bot ready, registering commands...");

        try
        {
            if ( _testGuildId.HasValue )
            {
                // Register to test guild for instant updates during development
                await _interactions.RegisterCommandsToGuildAsync(_testGuildId.Value);
                _logger.LogInformation("Slash commands registered to test guild {GuildId}", _testGuildId.Value);
            }
            else
            {
                // Register globally (takes ~1 hour to propagate)
                await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Slash commands registered globally");
            }
        }
        catch ( Exception ex )
        {
            _logger.LogError(ex, "Failed to register slash commands");
        }

        _readyTcs.TrySetResult();
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(Client, interaction);
            await _interactions.ExecuteCommandAsync(context, _services);
        }
        catch ( Exception ex )
        {
            _logger.LogError(ex, "Error executing interaction");

            // Respond with error if not already responded
            if ( interaction.Type == InteractionType.ApplicationCommand )
            {
                var cmdInteraction = (SocketSlashCommand)interaction;
                if ( !cmdInteraction.HasResponded )
                {
                    await cmdInteraction.RespondAsync("❌ An error occurred processing your command.", ephemeral: true);
                }
            }
        }
    }

    private Task OnMessageReceivedAsync(SocketMessage rawMessage)
    {
        // Only process for XP (no text commands)
        if ( rawMessage is not SocketUserMessage message || message.Author.IsBot )
            return Task.CompletedTask;

        _xpService.TryGrantXp(message.Author.Id, message.Channel.Id, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    private Task LogAsync(LogMessage log)
    {
        _logger.Log(log.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Information
            }, log.Exception, "[Discord] {Message}", log.Message
        );
        return Task.CompletedTask;
    }
}