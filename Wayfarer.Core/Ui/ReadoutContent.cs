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
/// <param name="StripLabel">What goes in the banner's header pill — the small dark strip above the
/// plate, where the game itself prints "Current Main Scenario Quest".
///
/// <para><b>It says what KIND of thing is being tracked, never which one.</b> "Current Quest",
/// "Current Unlock", "Current Hunting Log", or the plugin's own name when nothing is being followed.
/// The name of the actual thing is the subject line, which the banner draws on the plate itself, so
/// the pill and the plate are a category and an instance — exactly what the game's own pair
/// are.</para>
///
/// <para>The module's half of it is supplied by the guidance source that owns the arrow
/// (<see cref="Navigation.NavigationState.SourceName"/>) and is never derived from a source id here.
/// Nothing on the guidance path is allowed to know which features exist, and a switch on source ids
/// — in this composer or in the renderer — would put exactly that knowledge in the one place the
/// architecture keeps it out of.</para></param>
public sealed record ReadoutContent(
    IReadOnlyList<ReadoutLine> Lines,
    bool ShowArrow,
    float? TargetX = null,
    float? TargetY = null,
    float? TargetZ = null,
    ElevationHint Elevation = ElevationHint.Level,
    string StripLabel = ReadoutComposer.PluginName)
{
    /// <summary>Nothing to draw at all — the readout hides itself rather than showing a frame
    /// around emptiness.</summary>
    public static ReadoutContent Empty { get; } = new([], false);

    /// <summary>True when there is genuinely nothing to say.</summary>
    public bool IsEmpty => Lines.Count == 0;
}
