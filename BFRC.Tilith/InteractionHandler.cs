using System.Reflection;
using BFRC.Tilith.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace BFRC.Tilith;

public class InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services, IConfiguration config, LoggingService logging)
{
    public async Task InitializeAsync()
    {
        // Process when the client is ready, so we can register our commands.
        client.Ready += ReadyAsync;
        handler.Log += LoggingService.LogAsync;

        // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
        await handler.AddModulesAsync(Assembly.GetEntryAssembly(), services);

        // Process the InteractionCreated payloads to execute Interactions commands
        client.InteractionCreated += HandleInteraction;

        // Also process the result of the command execution.
        handler.InteractionExecuted += HandleInteractionExecute;
    }

    private async Task ReadyAsync()
    {
        // Register the commands globally.
        // alternatively you can use handler.RegisterCommandsGloballyAsync() to register commands to a specific guild.
        var commands =
#if DEBUG
            await handler.RegisterCommandsToGuildAsync(config.GetValue<ulong>("testGuild"));
#else
            await handler.RegisterCommandsGloballyAsync();
#endif

        foreach ( var command in commands )
            _ = logging.DebugAsync($"Name:{command.Name} Type.{command.Type} loaded");
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
            var context = new SocketInteractionContext(client, interaction);

            // Execute the incoming command.
            var result = await handler.ExecuteCommandAsync(context, services);

            // Due to async nature of InteractionFramework, the result here may always be success.
            // That's why we also need to handle the InteractionExecuted event.
            if ( !result.IsSuccess )
                switch ( result.Error )
                {
                    case InteractionCommandError.UnmetPrecondition:
                        // implement
                        break;
                }
        }
        catch
        {
            // If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
            // response, or at least let the user know that something went wrong during the command execution.
            if ( interaction.Type is InteractionType.ApplicationCommand )
                await interaction.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
    }

    private Task HandleInteractionExecute(ICommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if ( result.IsSuccess )
            return Task.CompletedTask;

        switch ( result.Error )
        {
            case InteractionCommandError.UnmetPrecondition:
                // implement
                break;
        }

        return Task.CompletedTask;
    }
}