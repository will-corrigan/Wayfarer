using Dalamud.Plugin.Services;
using Serilog;
using Serilog.Events;

namespace Wayfarer.App;

/// <summary>Dalamud's plugin log with a second destination: every event also reaches the
/// <see cref="RemoteLogSink"/>, which posts it to the LAN when a URL is configured. Registered as
/// a decorator, so every class asks for <see cref="IPluginLog"/> as before and gets this.</summary>
internal sealed class LanMirroredPluginLog(IPluginLog inner, RemoteLogSink remote) : IPluginLog
{
    private readonly IPluginLog inner = inner;

    /// <inheritdoc/>
    public ILogger Logger { get; } = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Logger(inner.Logger)
            .WriteTo.Sink(remote)
            .CreateLogger();

    /// <inheritdoc/>
    public LogEventLevel MinimumLogLevel
    {
        get => inner.MinimumLogLevel;
        set => inner.MinimumLogLevel = value;
    }

    /// <inheritdoc/>
    public void Fatal(string messageTemplate, params object[] values) => Logger.Fatal(messageTemplate, values);

    /// <inheritdoc/>
    public void Fatal(Exception? exception, string messageTemplate, params object[] values) => Logger.Fatal(exception, messageTemplate, values);

    /// <inheritdoc/>
    public void Error(string messageTemplate, params object[] values) => Logger.Error(messageTemplate, values);

    /// <inheritdoc/>
    public void Error(Exception? exception, string messageTemplate, params object[] values) => Logger.Error(exception, messageTemplate, values);

    /// <inheritdoc/>
    public void Warning(string messageTemplate, params object[] values) => Logger.Warning(messageTemplate, values);

    /// <inheritdoc/>
    public void Warning(Exception? exception, string messageTemplate, params object[] values) => Logger.Warning(exception, messageTemplate, values);

    /// <inheritdoc/>
    public void Information(string messageTemplate, params object[] values) => Logger.Information(messageTemplate, values);

    /// <inheritdoc/>
    public void Information(Exception? exception, string messageTemplate, params object[] values) => Logger.Information(exception, messageTemplate, values);

    /// <inheritdoc/>
    public void Info(string messageTemplate, params object[] values) => Logger.Information(messageTemplate, values);

    /// <inheritdoc/>
    public void Info(Exception? exception, string messageTemplate, params object[] values) => Logger.Information(exception, messageTemplate, values);

    /// <inheritdoc/>
    public void Debug(string messageTemplate, params object[] values) => Logger.Debug(messageTemplate, values);

    /// <inheritdoc/>
    public void Debug(Exception? exception, string messageTemplate, params object[] values) => Logger.Debug(exception, messageTemplate, values);

    /// <inheritdoc/>
    public void Verbose(string messageTemplate, params object[] values) => Logger.Verbose(messageTemplate, values);

    /// <inheritdoc/>
    public void Verbose(Exception? exception, string messageTemplate, params object[] values) => Logger.Verbose(exception, messageTemplate, values);

    /// <inheritdoc/>
    public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values) => Logger.Write(level, exception, messageTemplate, values);
}
