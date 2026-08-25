using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>Where the readout goes, as arithmetic — no game, no addons, no rendering.
///
/// Every rule the readout's position has to obey lives here so it can be tested rather than
/// eyeballed on a television: the ten-foot safe area, the clamp that makes it impossible to lose the
/// readout off the edge of the screen, the fraction that survives a resolution change, and the
/// "never sit on top of the minimap" rule that the old quest-tracker-following default violated.
///
/// The plugin supplies the live numbers (screen size, the minimap's and quest tracker's rectangles);
/// this decides what to do with them.</summary>
public static class ReadoutLayout
{
    /// <summary>Microsoft's ten-foot guidance, and the reason it is here: on a television the outer
    /// few percent of the panel is behind the bezel or lost to overscan. Every position this class
    /// can produce — preset, custom or dragged — is inside this margin.</summary>
    public const float SafeMarginX = 48f;

    /// <inheritdoc cref="SafeMarginX"/>
    public const float SafeMarginY = 27f;

    /// <summary>The gap left between the readout and a HUD element it has been pushed clear of.</summary>
    public const float ObstacleGap = 8f;

    /// <summary>The vertical extent every placement decision is made against, <b>instead of the
    /// readout's measured height</b>.
    ///
    /// <para><b>This is the whole of the "readout flies up and down the screen" fix, and it is worth
    /// being explicit about why a constant is the right answer.</b> Every function in this class used
    /// to take the readout's real, measured size, so where the readout sat was a function of how tall
    /// its content happened to be that frame. Content is not stable: a hunting summary, a teleport
    /// advice, an aethernet leg or a nearby-unlock line can each come and go as the world changes,
    /// and every one of them moved the readout — because the travel range, the clamp, the bottom
    /// anchors and the obstacle dodge all shrank or grew with it. A line that flickers on alternate
    /// frames therefore did not merely flicker: it made the whole readout jump, once per frame, for
    /// as long as it lasted.</para>
    ///
    /// <para>With the height fixed, the readout has a <b>slot</b> on the screen and grows downward
    /// inside it. A line appearing or disappearing changes where the readout <i>ends</i> and nothing
    /// else. There is no feedback path left from measured content to position, so no jitter in any
    /// input — a distance on a rounding boundary, a target that flickers in and out of live tracking,
    /// a HUD addon resizing — can move it at all.</para>
    ///
    /// <para>240 is the deepest readout the composer can produce (12 lines at the muted size, plus
    /// its rules and gaps) with room to spare, so the slot is never smaller than its content.</para></summary>
    public const float ReferenceHeight = 240f;

    /// <summary>The rectangle a readout is <i>placed</i> as, given what it actually measured. Width
    /// is real — it is a constant of the design, not of the content — and height is
    /// <see cref="ReferenceHeight"/>. Everything that decides where the readout goes takes this
    /// rather than the measurement.</summary>
    public static Vector2 PlacementSize(Vector2 measured) => new(measured.X, ReferenceHeight);

    /// <summary>The usable rectangle for the readout's top-left corner: the screen, less the safe
    /// margins, less the readout's own size. Collapses to a point rather than inverting when the
    /// readout is larger than the screen allows, so nothing downstream ever divides by a negative.</summary>
    public static Vector2 TravelRange(Vector2 size, Vector2 screen) => new(
        Math.Max(screen.X - size.X - (SafeMarginX * 2f), 0f),
        Math.Max(screen.Y - size.Y - (SafeMarginY * 2f), 0f));

    /// <summary>Pins a position inside the safe area. This is what makes it impossible to lose the
    /// readout: a stored position from a larger monitor, a resolution change mid-session and a drag
    /// towards the edge all come back through here.</summary>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Vector2 screen)
    {
        var range = TravelRange(size, screen);
        return new Vector2(
            Math.Clamp(position.X, SafeMarginX, SafeMarginX + range.X),
            Math.Clamp(position.Y, SafeMarginY, SafeMarginY + range.Y));
    }

    /// <summary>Turns a stored 0..1 position into screen pixels. Stored as a fraction of the usable
    /// range rather than as pixels precisely so that the readout is in the same <i>place</i> — a
    /// third of the way across, hard against the top — whatever resolution the game is running at.</summary>
    public static Vector2 FromFraction(Vector2 fraction, Vector2 size, Vector2 screen)
    {
        var range = TravelRange(size, screen);
        return new Vector2(
            SafeMarginX + (Math.Clamp(fraction.X, 0f, 1f) * range.X),
            SafeMarginY + (Math.Clamp(fraction.Y, 0f, 1f) * range.Y));
    }

    /// <summary>The inverse of <see cref="FromFraction"/>: what to store after a drag, or after the
    /// player has moved one of the position sliders.</summary>
    public static Vector2 ToFraction(Vector2 position, Vector2 size, Vector2 screen)
    {
        var range = TravelRange(size, screen);
        var clamped = Clamp(position, size, screen);
        return new Vector2(
            range.X <= 0f ? 0.5f : Math.Clamp((clamped.X - SafeMarginX) / range.X, 0f, 1f),
            range.Y <= 0f ? 0f : Math.Clamp((clamped.Y - SafeMarginY) / range.Y, 0f, 1f));
    }

    /// <summary>Where a preset puts the readout, before anything is dodged or clamped.
    /// <see cref="ReadoutPosition.FollowQuestTracker"/> and <see cref="ReadoutPosition.Custom"/> are
    /// not corners and are resolved by their own callers; both fall back to top centre here, which is
    /// the default placement and is clear of the minimap and the tracker on a default HUD.</summary>
    public static Vector2 Anchor(ReadoutPosition preset, Vector2 size, Vector2 screen)
    {
        var range = TravelRange(size, screen);
        var left = SafeMarginX;
        var centre = SafeMarginX + (range.X / 2f);
        var right = SafeMarginX + range.X;
        var top = SafeMarginY;
        var bottom = SafeMarginY + range.Y;

        return preset switch
        {
            ReadoutPosition.TopLeft => new Vector2(left, top),
            ReadoutPosition.TopRight => new Vector2(right, top),
            ReadoutPosition.BottomLeft => new Vector2(left, bottom),
            ReadoutPosition.BottomRight => new Vector2(right, bottom),
            ReadoutPosition.BottomCentre => new Vector2(centre, bottom),
            _ => new Vector2(centre, top),
        };
    }

    /// <summary>Hangs the readout off the game's own quest tracker, mirroring the way the tracker
    /// flips its own layout depending on which half of the screen it is on: below it on the left,
    /// right edges aligned on the right.</summary>
    public static Vector2 FollowTracker(ScreenRect tracker, Vector2 size, Vector2 screen)
    {
        var below = tracker.Bottom + ObstacleGap;
        var x = tracker.X < screen.X / 2f
            ? tracker.X
            : tracker.Right - size.X;

        return Clamp(new Vector2(x, below), size, screen);
    }

    /// <summary>Pushes the readout clear of the HUD elements it would otherwise sit on top of.
    ///
    /// <para>This is the fix for the reported defect rather than a nicety: with the readout following
    /// the quest tracker on a 16:9 television, its second line was drawn <b>behind the minimap</b> and
    /// simply could not be read. Preference is to move down — the readout grows downward, and the
    /// things it collides with (minimap, tracker) live at the top of the screen — and to move up only
    /// when down would run out of screen. If neither fits, the position is returned unchanged rather
    /// than shuffled somewhere arbitrary: a readable overlap beats a readout in a surprising
    /// place.</para>
    ///
    /// <para>Never applied to <see cref="ReadoutPosition.Custom"/>. Somewhere the player deliberately
    /// put the readout is not an accident to be corrected.</para></summary>
    public static Vector2 Avoid(
        Vector2 position, Vector2 size, Vector2 screen, IReadOnlyList<ScreenRect> obstacles)
    {
        ArgumentNullException.ThrowIfNull(obstacles);

        var result = Clamp(position, size, screen);

        // One settling pass per obstacle: moving clear of one can slide the readout into another,
        // and a fixed bound is what stops two overlapping HUD elements from looping forever.
        for (var pass = 0; pass < obstacles.Count; pass++)
        {
            var moved = false;
            foreach (var obstacle in obstacles)
            {
                if (!new ScreenRect(result, size).Overlaps(obstacle))
                {
                    continue;
                }

                var below = Clamp(result with { Y = obstacle.Bottom + ObstacleGap }, size, screen);
                if (!new ScreenRect(below, size).Overlaps(obstacle))
                {
                    result = below;
                    moved = true;
                    continue;
                }

                var above = Clamp(result with { Y = obstacle.Y - size.Y - ObstacleGap }, size, screen);
                if (!new ScreenRect(above, size).Overlaps(obstacle))
                {
                    result = above;
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        return result;
    }
}
