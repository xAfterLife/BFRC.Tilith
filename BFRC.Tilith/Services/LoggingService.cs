using System.Diagnostics;
using System.Runtime.CompilerServices;
using BFRC.Tilith.Enums;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace BFRC.Tilith.Services;

public class LoggingService
{
    private readonly LoggingFilterSeverity _loggingFilterSeverity = LoggingFilterSeverity.All;

    public LoggingService(IServiceProvider services)
    {
        services.GetRequiredService<CommandService>().Log += LogAsync;
    }

    public LoggingService(IServiceProvider services, LoggingFilterSeverity loggingFilterSeverity)
    {
        _loggingFilterSeverity = loggingFilterSeverity;

        services.GetRequiredService<CommandService>().Log += LogAsync;
        services.GetRequiredService<DiscordSocketClient>().Log += LogAsync;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
    public Task DebugAsync(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return LogAsync(LoggingSeverity.Debug, message, caller, file, line);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
    public Task InfoAsync(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return LogAsync(LoggingSeverity.Info, message, caller, file, line);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
    public Task WarningAsync(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return LogAsync(LoggingSeverity.Warning, message, caller, file, line);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
    public Task ErrorAsync(Exception? ex)
    {
        if ( ex == null )
            return Task.CompletedTask;

        var st = new StackTrace(ex, true);
        var sf = st.GetFrame(st.FrameCount - 1);

        return LogAsync(LoggingSeverity.Error, $"{ex.GetType().FullName} - {ex.Message}{Environment.NewLine}{ex.StackTrace}", sf!.GetMethod()!.Name, sf.GetFileName()!, sf.GetFileLineNumber());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
    private static bool ShouldLog(in LoggingSeverity loggingSeverity, in LoggingFilterSeverity loggingFilterSeverity)
    {
        return loggingFilterSeverity switch
        {
            LoggingFilterSeverity.All => true,
            LoggingFilterSeverity.NoDebug => loggingSeverity is not LoggingSeverity.Debug,
            LoggingFilterSeverity.Extended => loggingSeverity is LoggingSeverity.Warning or LoggingSeverity.Error,
            LoggingFilterSeverity.Production => loggingSeverity is LoggingSeverity.Error,
            LoggingFilterSeverity.None => false,
            _ => throw new ArgumentOutOfRangeException(nameof(loggingFilterSeverity), loggingFilterSeverity, null)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
    private Task LogAsync(LoggingSeverity loggingSeverity, string message = "", [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if ( string.IsNullOrEmpty(message) || !ShouldLog(in loggingSeverity, in _loggingFilterSeverity) )
            return Task.CompletedTask;

        Console.ForegroundColor = (ConsoleColor)loggingSeverity;
        Console.Write($@"{DateTime.Now.ToLongTimeString()} [{Path.GetFileNameWithoutExtension(file)}->{caller} L{line}] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($@"{message}{Environment.NewLine}");

        return Task.CompletedTask;
    }

    public static Task LogAsync(LogMessage log)
    {
        Console.ForegroundColor = log.Exception != null ? (ConsoleColor)LoggingSeverity.Error : ConsoleColor.Cyan;
        Console.Write($"{DateTime.Now.ToLongTimeString()} [{log.Source}] ");
        Console.ForegroundColor = ConsoleColor.White;

        if ( !string.IsNullOrEmpty(log.Message) )
            Console.WriteLine($"{log.Message}");
        if ( log.Exception != null )
            Console.WriteLine(log.Exception.ToString());

        return Task.CompletedTask;
    }
}