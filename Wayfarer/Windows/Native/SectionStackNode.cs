using System.Diagnostics.CodeAnalysis;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Wayfarer.Windows.Native;

/// <summary>A vertical stack whose height is its contents' height, and which cannot draw outside its
/// own bounds.
///
/// <para><b>What it is for.</b> This is the answer to the class of defect that produced "text printed
/// on top of the heading below it" over and over: a page whose blocks were placed at y-positions the
/// plugin computed, by measuring the block above and adding a gap. A wrapped string's height depends
/// on the font, the column width and where the words break, so a measurement taken at the wrong
/// moment — before the node had its real width, or before a paragraph had been shortened — moved
/// every block below it. There is no arithmetic here to get wrong: each child reports its own height
/// and the container places the next one after it. Layout is a consequence of content.</para>
///
/// <para><b>Belt as well as braces.</b> <see cref="LayoutListNode.ClipListContents"/> is on by
/// default, so even a child that lies about its own height — a text node whose measurement came back
/// short — is cut off at the stack's edge instead of being drawn over its neighbour. A wrong
/// measurement can cost a visible line; it can no longer cost a legible page.</para>
///
/// <para><b>Reusable on purpose, and reused.</b> It is deliberately not coupled to the journal, so
/// the readout (<see cref="ReadoutBodyNode"/>) — which used to place every one of its lines by hand
/// and carried the last manual y-accumulation in the plugin — could adopt it as a use rather than a
/// rewrite. Both surfaces now flow, and there is one definition of what flowing means.</para>
///
/// <para><b>Clipping is a default, not a rule.</b> The readout turns it off, because two of the
/// things it draws overhang the row they belong to on purpose — the game's own "!" medallion is 32
/// tall in a 26-tall row, and the crest rises above the plate — and because a hit box that is
/// partially clipped is, per <see cref="LayoutListNode.ClipListContents"/>'s own remark,
/// un-interactable. A readout whose teleport line cannot be clicked is a worse failure than a line
/// that draws a few pixels long.</para></summary>
[SuppressMessage("Performance", "CA1852:Seal internal types", Justification = OpenOnPurpose)]
internal class SectionStackNode : VerticalListNode
{
    /// <summary>Why this type is not sealed: it is the base a surface derives its own kind of section
    /// from, which is the whole reason it is not coupled to any one of them.</summary>
    private const string OpenOnPurpose =
        "Open on purpose: the base a surface derives its own kind of section from.";

    public SectionStackNode()
    {
        FitContents = true;
        ClipListContents = true;
        Anchor = VerticalListAnchor.Top;
        Alignment = VerticalListAlignment.Left;
    }

    /// <summary>Places the children, then repairs the one edge case the base class gets wrong: with
    /// nothing visible its <c>FitContents</c> sum comes out at minus one item spacing, and a node of
    /// negative height clips everything inside it including itself.</summary>
    protected override void OnRecalculateLayout()
    {
        base.OnRecalculateLayout();

        if (Height < 0f)
        {
            Height = 0f;
        }
    }
}
