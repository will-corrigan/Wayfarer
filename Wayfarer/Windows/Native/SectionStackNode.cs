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
/// <para><b>Reusable on purpose.</b> The readout (<see cref="ReadoutBodyNode"/>) places every one of
/// its lines by hand and carries the last manual y-accumulation left in the plugin. It is the
/// highest-value place for this node to go next, and it is deliberately not coupled to the journal so
/// that conversion is a use rather than a rewrite.</para></summary>
internal class SectionStackNode : VerticalListNode
{
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
