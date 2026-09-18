using Dalamud.Plugin.Services;

namespace Wayfarer.App;

/// <summary>Writes every line to the Dalamud log and hands it to the remote sink.</summary>
internal sealed class Log(IPluginLog pluginLog, RemoteLogSink remote) : ILog
{
    private const string Prefix = "Wayfarer: ";

    /// <inheritdoc/>
    public void Debug(string message)
    {
        pluginLog.Debug(Prefix + message);
        remote.Offer("debug", message);
    }

    /// <inheritdoc/>
    public void Info(string message)
    {
        pluginLog.Information(Prefix + message);
        remote.Offer("info", message);
    }

    /// <inheritdoc/>
    public void Warning(string message, Exception? exception = null)
    {
        pluginLog.Warning(exception, Prefix + message);
        remote.Offer("warning", exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}");
    }

    /// <inheritdoc/>
    public void Error(string message, Exception? exception = null)
    {
        pluginLog.Error(exception, Prefix + message);
        remote.Offer("error", exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}");
    }
}
