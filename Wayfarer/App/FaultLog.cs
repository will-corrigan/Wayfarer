using System.Globalization;
using Dalamud.Plugin.Services;

namespace Wayfarer.App;

/// <summary>Keeps the last few faults as lines a player can read, and writes each to the Dalamud
/// log as it comes in.</summary>
internal sealed class FaultLog(IPluginLog log) : IFaultLog
{
    private const int Kept = 5;
    private const string TimeFormat = "HH:mm:ss";

    private readonly List<string> recent = [];

    /// <inheritdoc/>
    public IReadOnlyList<string> Recent => recent;

    /// <inheritdoc/>
    public void Record(string where, Exception exception)
    {
        log.Error(exception, $"Wayfarer: {where}");
        Keep($"{where}: {exception.GetType().Name}: {exception.Message}");
    }

    /// <inheritdoc/>
    public void Record(string where, string what)
    {
        log.Warning($"Wayfarer: {where}: {what}");
        Keep($"{where}: {what}");
    }

    private void Keep(string line)
    {
        recent.Insert(0, $"{DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture)} {line}");
        if (recent.Count > Kept)
        {
            recent.RemoveAt(recent.Count - 1);
        }
    }
}
