using System.Diagnostics;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tilith.Core.Models;
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
    private bool _modulesLoaded;

    public TilithBot(IConfiguration config, IServiceProvider services, ILogger<TilithBot> logger, XpService xpService)
    {
        _token = config["Discord:Token"] ?? throw new InvalidOperationException("Discord token missing");
        _testGuildId = config.GetValue<ulong?>("Discord:TestGuild");
        _services = services;
        _logger = logger;
        _xpService = xpService;

        Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent, LogLevel = LogSeverity.Info,
                MessageCacheSize = 100,
                ConnectionTimeout = 10000
            }
        );

        _interactions = new InteractionService(Client.Rest, new InteractionServiceConfig
            {
                DefaultRunMode = RunMode.Async,
                UseCompiledLambda = true,
                ThrowOnError = true
            }
        );

        Client.MessageReceived += OnMessageReceivedAsync;
        Client.Ready += OnReadyAsync;
        Client.InteractionCreated += OnInteractionCreatedAsync;
        Client.Log += LogAsync;
        _interactions.Log += LogAsync;
    }

    public DiscordSocketClient Client { get; }

    public async Task StartAsync(CancellationToken ct)
    {
        // CRITICAL: Load modules BEFORE starting client
        if ( !_modulesLoaded )
        {
            var modules = await _interactions.AddModulesAsync(typeof(TilithBot).Assembly, _services);
            var moduleInfos = modules as ModuleInfo[] ?? modules.ToArray();

            _modulesLoaded = true;
            _logger.LogInformation("Loaded {Count} interaction modules: {Modules}",
                moduleInfos.Length, string.Join(", ", moduleInfos.Select(m => m.Name))
            );

            if ( moduleInfos.Length == 0 )
            {
                throw new InvalidOperationException("No interaction modules loaded. Check assembly scanning.");
            }
        }

        await Client.LoginAsync(TokenType.Bot, _token);
        await Client.StartAsync();
        _logger.LogInformation("Discord client started, waiting for Ready event...");
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
        _logger.LogInformation("Discord Ready event fired. Guilds: {Count}, Latency: {Latency}ms",
            Client.Guilds.Count, Client.Latency
        );

        try
        {
            if ( _testGuildId.HasValue )
            {
                var commands = await _interactions.RegisterCommandsToGuildAsync(_testGuildId.Value);
                _logger.LogInformation("Registered {Count} commands to guild {GuildId}: {Commands}",
                    commands.Count, _testGuildId.Value, string.Join(", ", commands.Select(c => c.Name))
                );
            }
            else
            {
                var commands = await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Registered {Count} global commands: {Commands}",
                    commands.Count, string.Join(", ", commands.Select(c => c.Name))
                );
            }

            _readyTcs.TrySetResult();
        }
        catch ( Exception ex )
        {
            _logger.LogCritical(ex, "FATAL: Failed to register slash commands");
            _readyTcs.TrySetException(ex);
            throw; // Propagate to crash the worker
        }
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var context = new SocketInteractionContext(Client, interaction);
            var result = await _interactions.ExecuteCommandAsync(context, _services);

            if ( !result.IsSuccess )
            {
                _logger.LogError("Command execution failed: {Error}", result.ErrorReason);
                if ( interaction is { Type: InteractionType.ApplicationCommand, HasResponded: false } )
                {
                    await interaction.RespondAsync($"❌ Error: {result.ErrorReason}", ephemeral: true);
                }
            }
            else
            {
                _logger.LogDebug("Command executed in {Ms}ms", sw.ElapsedMilliseconds);
            }
        }
        catch ( Exception ex )
        {
            _logger.LogError(ex, "Exception executing interaction {Id}", interaction.Id);
            if ( interaction.Type == InteractionType.ApplicationCommand )
            {
                var cmdInteraction = (SocketSlashCommand)interaction;
                if ( !cmdInteraction.HasResponded )
                {
                    await cmdInteraction.RespondAsync("❌ An unexpected error occurred.", ephemeral: true);
                }
            }
        }
    }

    private Task OnMessageReceivedAsync(SocketMessage rawMessage)
    {
        if ( rawMessage is not SocketUserMessage message || message.Author.IsBot )
            return Task.CompletedTask;

        _ = Task.Run(async () =>
            {
                try
                {
                    var author = message.Author;
                    var displayName = author is SocketGuildUser guildUser
                        ? guildUser.DisplayName // Server nickname or global display name
                        : author.GlobalName ?? author.Username;

                    var metadata = new MessageMetadata(
                        author.Username,
                        displayName
                    );

                    await _xpService.TryGrantXpAsync(
                        author.Id,
                        message.Channel.Id,
                        DateTime.UtcNow,
                        metadata, // Pass metadata
                        CancellationToken.None
                    );
                }
                catch ( Exception ex )
                {
                    _logger.LogError(ex, "Failed to grant XP for user {UserId}", message.Author.Id);
                }
            }
        );

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