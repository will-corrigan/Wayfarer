namespace Wayfarer.Core.Ui;

/// <summary>Everything the guidance readout should show this frame, in order. Produced by
/// <see cref="ReadoutComposer"/> and rendered without further decisions — which is what lets the
/// "one active objective" rule be tested rather than eyeballed.</summary>
/// <param name="Lines">Heading first, then the objective, then advice, then muted context.</param>
/// <param name="ShowArrow">Whether a direction indicator should be drawn. Never true for more than
/// one thing: the arrow follows whatever the arbiter says is active, and nothing else.</param>
/// <param name="TargetX">World X of the thing the arrow points at, when there is one.</param>
/// <param name="TargetY">World Y, or null to treat the target as level with the player.</param>
/// <param name="TargetZ">World Z of the thing the arrow points at, when there is one.</param>
/// <param name="Elevation">Whether the target is meaningfully above or below the player, decided by
/// <see cref="Ui.Elevation.Classify"/> before it gets here. The distance line already says it in
/// words; this is what lets the drawn readout hang the game's own up/down chevron off the arrow as
/// well.</param>
public sealed record ReadoutContent(
    IReadOnlyList<ReadoutLine> Lines,
    bool ShowArrow,
    float? TargetX = null,
    float? TargetY = null,
    float? TargetZ = null,
    ElevationHint Elevation = ElevationHint.Level)
{
    /// <summary>Nothing to draw at all — the readout hides itself rather than showing a frame
    /// around emptiness.</summary>
    public static ReadoutContent Empty { get; } = new([], false);

    /// <summary>True when there is genuinely nothing to say.</summary>
    public bool IsEmpty => Lines.Count == 0;
}
