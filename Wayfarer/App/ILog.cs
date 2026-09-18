namespace Wayfarer.App;

/// <summary>The plugin's one way to say something: to the Dalamud log always, and to the remote
/// log when one is configured, so a tester who cannot send logs can still be read.</summary>
internal interface ILog
{
    void Debug(string message);

    void Info(string message);

    void Warning(string message, Exception? exception = null);

    void Error(string message, Exception? exception = null);
}
