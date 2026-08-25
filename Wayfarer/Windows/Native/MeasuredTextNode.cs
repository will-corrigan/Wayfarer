using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

// The node's own TextFlags property shadows the enum type inside this class, so the type needs a name
// of its own for the two flag calls below.
using Flags = FFXIVClientStructs.FFXIV.Component.GUI.TextFlags;

namespace Wayfarer.Windows.Native;

/// <summary>A wrapping block of text that knows how tall it is, so the container above it never has
/// to guess.
///
/// <para><b>The contract.</b> <see cref="Set"/> writes the string, asks the game how tall it actually
/// draws at this node's width, and puts that answer in the node's own
/// <see cref="KamiToolKit.BaseTypes.NodeBase.Height"/>. That is the only place a text height is
/// measured, and the only thing that consumes it is the node itself — a
/// <see cref="SectionStackNode"/> then places the next block after this one's height with no
/// arithmetic of its own. The measurement can still be wrong; what it can no longer do is move
/// anything else, because nothing else reads it.</para>
///
/// <para><b>Why the width has to be set before the measurement.</b> The wrap is computed against the
/// node's width, so a measurement taken while the node is still its default width is a measurement of
/// a different paragraph. Getting that order wrong is the single most common way the old page came to
/// draw over itself, so the order lives inside this node rather than at each of its call sites.</para>
///
/// <para><b>Bounded above.</b> <see cref="MaxHeight"/> is a cap on how far the block may grow, which
/// is what keeps the page's own height finite. Past the cap the string is shortened a word at a time
/// and marked with an ellipsis, and if even one wrapped line will not fit, the wrap is dropped and
/// the node ellipsises a single line — a floor that is bounded by construction, which is what makes
/// the loop safe to give up on.</para></summary>
internal sealed class MeasuredTextNode : TextNode
{
    /// <summary>The widest a block may be asked for. The game stores a node's width in an unsigned
    /// sixteen-bit field, and a number past that ceiling means the arithmetic that produced it has
    /// gone wrong rather than that a column really is that wide.</summary>
    private const float MaxWidth = 65535f;

    /// <summary>How tall the block may grow before its text is shortened. Zero means unbounded.
    /// </summary>
    public float MaxHeight { get; set; }

    /// <summary>Writes <paramref name="text"/>, measures it, and sizes the node to what it measured.
    /// An empty string hides the node, which is what takes it out of the stack entirely rather than
    /// leaving a hole where it used to be.</summary>
    public void Set(string text, float width)
    {
        // Assigned only once it is known to be a width the game can be given: it lands in an
        // unsigned sixteen-bit field, so a negative or non-finite one does not mean "narrow", it
        // means something enormous and arbitrary.
        var usable = float.IsFinite(width) && width > 0f && width <= MaxWidth;
        if (usable)
        {
            Width = width;
        }

        if (string.IsNullOrEmpty(text) || !usable)
        {
            String = string.Empty;
            Height = 0f;
            IsVisible = false;
            return;
        }

        IsVisible = true;
        var measured = Measure(text);
        if (MaxHeight <= 0f || measured <= MaxHeight)
        {
            Height = measured;
            return;
        }

        // Over the cap. A paragraph has no line structure to give up, so words off the end is the
        // only unit there is; the mark says the cut happened rather than leaving the player to
        // wonder whether the sentence ended there.
        var pieces = text.Split(' ');
        for (var take = pieces.Length - 1; take >= 1; take--)
        {
            measured = Measure(string.Join(' ', pieces.Take(take)) + "…");
            if (measured <= MaxHeight)
            {
                Height = measured;
                return;
            }
        }

        Truncate(text);
    }

    /// <summary>Sets the string and asks the game how tall it draws at this node's width. Restores
    /// the wrapping flags first: an earlier <see cref="Truncate"/> may have taken them away.
    /// </summary>
    private float Measure(string text)
    {
        RemoveTextFlags(Flags.Ellipsis);
        AddTextFlags(Flags.MultiLine, Flags.WordWrap);

        // Tall enough that the node never clips its own measurement: GetTextDrawSize reports what
        // the string would draw as, and a node shorter than that has been observed to report the
        // clipped figure instead.
        Height = MaxHeight is > 0f and <= MaxWidth ? MaxHeight : JournalWindowLayout.BlockHeight(1);
        String = text;

        // The measurement is the game's own, and it is the one number on this node nobody here
        // wrote. A figure that is not a usable height falls back to one line, which is a height the
        // stack above can always place.
        var drawn = GetTextDrawSize(considerScale: false).Y;
        return float.IsFinite(drawn) && drawn > 0f && drawn <= MaxWidth
            ? drawn
            : JournalWindowLayout.BlockHeight(1);
    }

    /// <summary>The last resort: drop the wrap, keep one line, let the node ellipsise it.</summary>
    private void Truncate(string text)
    {
        RemoveTextFlags(Flags.MultiLine, Flags.WordWrap);
        AddTextFlags(Flags.Ellipsis);
        String = text.ReplaceLineEndings(" ");
        Height = JournalWindowLayout.BlockHeight(1);
    }
}
