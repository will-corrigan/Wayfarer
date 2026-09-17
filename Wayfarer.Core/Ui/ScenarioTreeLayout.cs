namespace Wayfarer.Core.Ui;

/// <summary>The geometry of the block Wayfarer draws under the game's own Main Scenario Guide: the
/// readout's subordinate lines and their compass, with no banner above them because the game
/// draws its own.
///
/// <para>Every helper is <see cref="ReadoutBodyLayout"/>'s — the same line heights, the same gutter,
/// the same walk — so a proof about the readout's lines is a proof about these. What this class adds
/// is where the block sits inside the game's root, which is <see cref="GameMetrics.ScenarioTree"/>'s
/// business.</para></summary>
public static class ScenarioTreeLayout
{
    /// <summary>The block's left edge inside the game's root node.</summary>
    public static float Left => GameMetrics.ScenarioTree.GuidanceLeft;

    /// <summary>The block's top edge inside the game's root node, under whichever job-quest rows
    /// the game is showing.</summary>
    public static float Top(int visibleJobRows) => GameMetrics.ScenarioTree.GuidanceTop(visibleJobRows);

    /// <summary>Arranges the block at one scale, for one set of lines — the same arrangement the live
    /// node produces, from the same helpers.</summary>
    public static ScenarioTreeBlocks Compose(ScenarioTreeLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var factor = Math.Max(request.Factor, 0f);
        var width = ReadoutBodyLayout.Width(factor);
        var lines = request.Lines;

        var heights = new float[lines.Count + 1];
        for (var i = 0; i < lines.Count; i++)
        {
            heights[i] = ReadoutBodyLayout.LineHeight(lines[i], factor, ReadoutBodyLayout.GutterLine(i));
        }

        heights[^1] = ReadoutBodyLayout.FootHeight(factor);

        var height = ReadoutBodyLayout.FlowHeight(heights);
        var placed = ReadoutBodyLayout.Flow(heights, new ScreenRect(0f, 0f, width, height));
        var parts = ReadoutBodyLayout.ComposeLines(lines, factor, placed, firstSection: 0, request.Arrow, markers: true);

        // No lines at all and still something to point at: the compass parks where the first line
        // would have been, on the block's own first pitch.
        var arrowCentre = parts.ArrowCentre;
        if (request.Arrow && arrowCentre is null)
        {
            arrowCentre = placed[^1].Y + (GameMetrics.Banner.SubLinePitch * factor / 2f);
        }

        return new ScenarioTreeBlocks
        {
            Height = height,
            Arrow = arrowCentre is { } centre ? ReadoutBodyLayout.Arrow(centre, factor, request.ArrowScale) : default,
            Sections = parts.Sections,
            Rules = parts.Rules,
            Markers = parts.Markers,
            Texts = parts.Texts,
        };
    }
}
