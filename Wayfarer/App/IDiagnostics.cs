namespace Wayfarer.App;

/// <summary>Something that can write what it knows to the log on request, for the player at the
/// keyboard to read back when a thing that cannot be tested here misbehaves in the game.</summary>
internal interface IDiagnostics
{
    /// <summary>Writes the current state to the Dalamud log at information level.</summary>
    void Dump();
}
