using Wayfarer.Guidance;

namespace Wayfarer.Tests;

/// <summary>A module's guidance half, stood in for: it has a name and is never asked anything,
/// because the tests that use it hand the guidance in ready-made.</summary>
internal sealed class FakeSource(string name = "Quests") : IObjectiveSource
{
    /// <inheritdoc/>
    public string Name => name;

    /// <inheritdoc/>
    public Objective? Current => null;

    /// <inheritdoc/>
    public void Displaced()
    {
    }
}
