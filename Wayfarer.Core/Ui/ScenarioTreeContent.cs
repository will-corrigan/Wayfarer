using Wayfarer.Core.Navigation;

namespace Wayfarer.Core.Ui;

/// <summary>What Wayfarer writes under the game's own Main Scenario Guide: the objective's step and
/// the route to it, and nothing else.
///
/// <para><b>Why a second composer rather than a flag on the first.</b> The game's banner already
/// says what kind of thing is being followed and what its name is, so the heading and the subject
/// line the readout used to draw have nothing left to say. And the block sits inside the game's
/// own element, where a hunting summary or a list of nearby unlocks would be Wayfarer talking about
/// other features on the game's surface. The lines that remain are the readout's own — built by the
/// same methods, in the same order, with the same glyph-and-action rule — so this composer chooses
/// which lines to keep and never rewords one.</para>
///
/// <para>Deliberately not gated on the snapshot's source id. Nothing on the guidance path is
/// allowed to know which features exist (see <see cref="ReadoutContent.StripLabel"/>); which sources
/// can own the arrow is decided where sources are registered.</para></summary>
public static class ScenarioTreeContent
{
    /// <summary>The lines to draw this frame, or <see cref="ReadoutContent.Empty"/> when there is
    /// nothing to say — in which case the block hides rather than drawing a gap under the banner.
    /// </summary>
    public static ReadoutContent Compose(ReadoutInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var state = inputs.State;
        if (string.Equals(state.Mode, NavigationState.Modes.Hidden, StringComparison.Ordinal))
        {
            return ReadoutContent.Empty;
        }

        var lines = new List<ReadoutLine>();
        if (ReadoutComposer.StepLine(state) is { } step)
        {
            lines.Add(step);
        }

        var (showArrow, x, y, z) = ReadoutComposer.AddRoute(lines, inputs);
        return lines.Count == 0
            ? ReadoutContent.Empty
            : new ReadoutContent(lines, showArrow, x, y, z, inputs.Elevation);
    }
}
