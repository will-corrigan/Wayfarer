namespace Wayfarer.App;

/// <summary>Something that can write what it knows to the log on request, and flip an
/// investigation switch, for a thing that cannot be tested here and misbehaves in the game.</summary>
internal interface IDiagnostics
{
    /// <summary>Writes the current state to the log.</summary>
    void Dump();

    /// <summary>Flips the named switch and says what it is now, or null when the name is unknown.</summary>
    string? Toggle(string switchName);
}
