namespace Wayfarer.Core.Ui;

/// <summary>The Journal's gilt border, assembled the way the game assembles it.
///
/// <para><b>The whole of the argument for wearing it.</b> The earlier survey found the border is
/// sixteen nodes at hard-coded positions inside a page that is 496 wide, and advised against it —
/// correctly, because the advice was about stretching that run across a window whose width the
/// player drags. Nothing about the border can be stretched horizontally: fourteen of the sixteen
/// pieces are plain images drawn at the size their art is authored at, and the horizontal run tiles
/// 496 exactly. A window that <i>is</i> 496 wide therefore wears it with no adaptation at all. The
/// two vertical rails (<c>#13</c>/<c>#14</c>) are nine-grids with one-pixel caps, and they are what
/// the game itself uses to make the border any height it likes — so height is free and width is
/// fixed. That is the shape of this class.</para>
///
/// <para><b>Read from the file, not remembered.</b> Every number below is JournalDetail's own:
/// node <c>#11</c>'s children with their positions, sizes, part ids and flip flags, and part list
/// 10's rectangles on <c>ui/uld/Journal_Frame.tex</c>. The assembly was rendered to a PNG at 496x628
/// and looked at before any of it was written into a node.</para></summary>
public static class JournalFrameLayout
{
    /// <summary>Every piece of the border for a frame of <paramref name="height"/>, in the frame's
    /// own space. Shorter than <see cref="GameMetrics.JournalFrame.MinHeight"/> returns the pieces
    /// with the rails empty rather than refusing: a resize is not atomic and a layout pass can run
    /// against a height that is still on its way somewhere.</summary>
    public static IReadOnlyList<JournalFramePiece> Pieces(float height)
    {
        var h = Math.Max(height, 0f);
        var railHeight = Math.Max(h - GameMetrics.JournalFrame.MinHeight, 0f);

        // The bottom band is anchored to the foot rather than flowed down from the top, which is
        // what makes the rails — and only the rails — absorb the height.
        var foot = Math.Max(h - GameMetrics.JournalFrame.BottomFixed, GameMetrics.JournalFrame.TopFixed);

        // The top edge, left to right: #18/#26 the corners, #16/#27 the ornamental runs, #17/#28
        // the plain bars, #15 the centre boss. #17 and #28 are the same part unflipped because the
        // bar's art is symmetrical. Then the sides — a short ornamental run under each top corner
        // and the rail below it — and last the foot, all of it hung off the frame's bottom edge.
        return
        [
            Image(18, 0f, 8f, 56f, 88f, 0f, 0f),
            Image(16, 56f, 8f, 104f, 48f, 56f, 0f),
            Image(17, 160f, 8f, 48f, 24f, 64f, 80f),
            Image(15, 208f, 0f, 80f, 32f, 64f, 48f),
            Image(28, 288f, 8f, 48f, 24f, 64f, 80f),
            Image(27, 336f, 8f, 104f, 48f, 56f, 0f, flip: true),
            Image(26, 440f, 8f, 56f, 88f, 0f, 0f, flip: true),

            Image(19, 0f, 96f, 40f, 96f, 200f, 8f),
            Image(25, 456f, 96f, 40f, 96f, 200f, 8f, flip: true),
            Rail(14, 0f, railHeight, 168f, 56f),
            Rail(13, 464f, railHeight, 168f, 8f),

            Image(20, 0f, foot, 56f, 96f, 0f, 96f),
            Image(21, 56f, foot + 48f, 136f, 48f, 56f, 144f),
            Image(22, 192f, foot + 56f, 112f, 40f, 64f, 104f),
            Image(23, 304f, foot + 48f, 136f, 48f, 56f, 144f, flip: true),
            Image(24, 440f, foot, 56f, 96f, 0f, 96f, flip: true),
        ];
    }

    /// <summary>The parchment the border is nailed to — JournalDetail <c>#54</c>, a nine-grid that
    /// starts ten pixels down the border and runs to its foot.</summary>
    public static ScreenRect Parchment(float height)
    {
        var top = GameMetrics.JournalFrame.ParchmentTop;
        var h = Math.Max(height, 0f) - top;
        return h <= 0f ? default : new ScreenRect(0f, top, GameMetrics.JournalFrame.Width, h);
    }

    /// <summary>The box inside the border's rails. Everything the window draws has to live in here,
    /// which is what the containment proof asserts.</summary>
    public static ScreenRect Inner(float height)
    {
        var rail = GameMetrics.JournalFrame.RailWidth;
        var h = Math.Max(height, 0f);
        var width = GameMetrics.JournalFrame.Width - (rail * 2f);
        return h <= 0f ? default : new ScreenRect(rail, 0f, width, h);
    }

    private static JournalFramePiece Image(
        int node, float x, float y, float width, float height, float u, float v, bool flip = false) =>
        new(
            node,
            new ScreenRect(x, y, width, height),
            new ScreenRect(u, v, width, height),
            flip,
            Stretches: false);

    /// <summary>One of the two vertical rails: a 32x40 nine-grid with a one-pixel cap top and
    /// bottom, drawn from y=192 for whatever height is left over.</summary>
    private static JournalFramePiece Rail(int node, float x, float height, float u, float v) =>
        new(
            node,
            new ScreenRect(x, GameMetrics.JournalFrame.TopFixed, GameMetrics.JournalFrame.RailWidth, height),
            new ScreenRect(u, v, GameMetrics.JournalFrame.RailWidth, 40f),
            FlipHorizontally: false,
            Stretches: true);
}
