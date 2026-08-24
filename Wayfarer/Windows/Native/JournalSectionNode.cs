using System.Numerics;
using Dalamud.Plugin.Services;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>One of the journal page's sections: a glyph and a heading, with a body under it.
///
/// <para><b>The shape is the game's.</b> JournalCanvas builds Reward, Description and Requirements
/// identically — a 24x24 disc at the section's left edge
/// (<c>#6</c>/<c>#10</c>/<c>#19</c>/<c>#28</c>), the heading two pixels past it (<c>#7</c>/<c>#11</c>/
/// <c>#20</c>/<c>#29</c>), and the body indented to the same inset the banner and the tray sit at
/// (x=18). One class rather than three so the three cannot drift apart.</para>
///
/// <para><b>Why it is a stack and not a set of coordinates.</b> The body is the part whose height
/// nobody can know in advance, and a section is exactly the boundary at which that stops mattering:
/// the heading is placed, the body is placed after it, and the section's own height is the sum. The
/// page above then places the next section after this one's height. Nothing computes an offset from a
/// measurement of anything else, and the stack clips its own contents, so a body that measured short
/// is cut off at the section's edge rather than drawn over the section below.</para>
///
/// <para><b>A heading with nothing under it is refused whole.</b> <see cref="SetBody"/> hides the
/// entire section when the body is empty. A section that says only its own name is worse than no
/// section — it reads as content that failed to load.</para></summary>
internal sealed class JournalSectionNode : SectionStackNode
{
    private readonly HorizontalListNode headingRow;
    private readonly HorizontalListNode bodyRow;

    public JournalSectionNode(IPluginLog log, (float U, float V) glyph, string heading)
    {
        ArgumentNullException.ThrowIfNull(log);

        Width = JournalWindowLayout.ContentWidth;
        ItemSpacing = 0f;

        headingRow = new HorizontalListNode
        {
            Alignment = HorizontalListAnchor.Left,
            Width = JournalWindowLayout.ContentWidth,
            Height = GameMetrics.Journal.SectionHeadingHeight,
            FitToContentHeight = false,
        };

        var glyphNode = JournalNodes.Art(null, log, glyph, GameMetrics.Journal.GlyphSize);
        glyphNode.IsVisible = true;

        var label = JournalNodes.Heading(null, heading);
        label.Size = new Vector2(
            JournalWindowLayout.ContentWidth - GameMetrics.Journal.GlyphTextLeft,
            GameMetrics.Journal.SectionHeadingHeight);

        // The heading text starts two pixels under the glyph's right edge, which is the glyph art's
        // own transparent margin rather than an overlap — JournalCanvas does the same.
        headingRow.ItemSpacing =
            GameMetrics.Journal.GlyphTextLeft - GameMetrics.Journal.GlyphSize;
        JournalNodes.AddOnce(headingRow, glyphNode, label);

        bodyRow = new HorizontalListNode
        {
            Alignment = HorizontalListAnchor.Left,
            FirstItemSpacing = GameMetrics.Journal.SectionInset,
            Width = JournalWindowLayout.ContentWidth,
            FitToContentHeight = true,
        };

        JournalNodes.AddOnce(this, headingRow, bodyRow);
    }

    /// <summary>The row the section's body goes in, already indented to the game's own section inset.
    /// The caller adds one node to it — a paragraph, or a tray.</summary>
    public HorizontalListNode BodyRow() => bodyRow;

    /// <summary>Writes the section's prose and shows or hides the whole section accordingly. The body
    /// node measures itself; this only decides whether there is a section at all.</summary>
    public void SetBody(MeasuredTextNode body, string text, float width)
    {
        body.Set(text, width);
        IsVisible = body.IsVisible;
        RecalculateLayout();
    }
}
