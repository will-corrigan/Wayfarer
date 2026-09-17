namespace Wayfarer.Core.Ui;

/// <summary>Everything <see cref="ScenarioTreeLayout.Compose"/> needs to know about a frame of the
/// block under the game's own banner. The same shape as <see cref="ReadoutBodyRequest"/> with the
/// banner's own parts gone: there is no plate, no pill, no cog and no switcher to place, because
/// the game draws its own.</summary>
public sealed record ScenarioTreeLayoutRequest
{
    /// <summary>The scale every ULD unit is multiplied by. The game already renders the addon at the
    /// player's interface size, so this is the player's own text-size preference and nothing else.
    /// </summary>
    public float Factor { get; init; } = 1f;

    /// <summary>The lines, in reading order.</summary>
    public IReadOnlyList<ReadoutBlock> Lines { get; init; } = [];

    /// <summary>Whether the compass is on screen. It sits in the gutter beside the first line and
    /// takes no vertical space of its own.</summary>
    public bool Arrow { get; init; }

    /// <summary>The player's arrow-size preference, clamped to half..double by the layout.</summary>
    public float ArrowScale { get; init; } = 1f;
}
