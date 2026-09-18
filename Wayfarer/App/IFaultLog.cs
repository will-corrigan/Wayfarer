namespace Wayfarer.App;

/// <summary>The plugin's own record of what went wrong, kept so the settings window can show it
/// to a player who cannot open the Dalamud log or send it anywhere. Everything recorded here is
/// also written to the Dalamud log.</summary>
internal interface IFaultLog
{
    /// <summary>The most recent faults, newest first, as one line each.</summary>
    IReadOnlyList<string> Recent { get; }

    /// <summary>Records an exception with where it happened.</summary>
    void Record(string where, Exception exception);

    /// <summary>Records a refusal or a warning with no exception behind it.</summary>
    void Record(string where, string what);
}
