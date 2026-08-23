using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace Wayfarer.Windows.Native;

/// <summary>The view for <see cref="HubListRow"/>. Built from plain <c>TextNode</c>s, which are
/// structurally incapable of holding cursor-navigation info — <c>ListItemWithFocusNav</c> is what
/// smuggles a zero-size component into the row so it can own a nav slot and turn a confirm press
/// into an activation. Confirm arrives as the <b>logical</b> <c>InputId.OK</c>, so the player's own
/// PadReverseConfirmCancel setting is honoured without us reading it.
///
/// <para><b>Shape.</b> Two lines, mirroring the Duty Finder's own tree-list row: name on line one
/// with a short right-hand caption, and a dimmed qualifying line underneath. The previous row was a
/// single 26px line that gave the name ~590px it did not need and squeezed zone, level and state
/// into a 132px gutter, ellipsising all three — 90% of the row was blank and the 10% that carried
/// meaning was the part that got cut. On a TV at 200% HUD scale it was unreadable before it was
/// truncated.</para></summary>
internal sealed class HubListRowNode : ListItemWithFocusNav<HubListRow>, IListItemNode
{
    private const float Padding = 8f;

    /// <summary>Width of the right-hand caption on line one. Wider than the old gutter because it
    /// now carries two short tokens instead of three facts, and the name beside it has plenty to
    /// give up.</summary>
    private const float TrailingWidth = 168f;

    private const float LineGap = 6f;

    private readonly TextNode labelNode;
    private readonly TextNode trailingNode;
    private readonly TextNode descriptionNode;

    private HubRowKind kind = HubRowKind.Entry;

    public HubListRowNode()
    {
        labelNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 14,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.ListText,
        };
        labelNode.AttachNode(this);

        trailingNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 12,
            AlignmentType = AlignmentType.Right,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        trailingNode.AttachNode(this);

        descriptionNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 12,
            AlignmentType = AlignmentType.Left,

            // Ellipsis rather than a hand-rolled character budget: the game's own text engine
            // measures in the font that is actually being drawn, at the size it is actually being
            // drawn, which a character count cannot do and which is exactly how a "safe" budget
            // ends up cutting a word in half on one HUD scale and leaving a third of the row empty
            // on another.
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        descriptionNode.AttachNode(this);
    }

    /// <summary>Row height, a per-type constant because the list virtualizes on it. Two lines of
    /// Axis at 14/12pt with the game's own row padding, which halves the visible row count against
    /// the old 26px — deliberately: ten-foot guidance is "density comparable to a phone, not a
    /// desktop", and the section headings already in the list are what keep a long one navigable.
    ///
    /// <para>Deliberately not a setting. A knob for row height is a design decision being deferred
    /// to the player, and the number wants changing once after somebody has looked at a TV, not
    /// per install.</para></summary>
    public static float ItemHeight => 44f;

    /// <inheritdoc/>
    public override void Update()
    {
        if (ItemData is { } data)
        {
            trailingNode.String = data.Detail;
            descriptionNode.String = data.Description;
        }
    }

    /// <inheritdoc/>
    protected override void SetNodeData(HubListRow itemData)
    {
        kind = itemData.Kind;

        labelNode.String = itemData.Label;
        trailingNode.String = itemData.Detail;
        descriptionNode.String = itemData.Description;

        labelNode.TextColor = itemData.LabelColor ?? DefaultColor(itemData.Kind);
        labelNode.FontSize = itemData.Kind switch
        {
            HubRowKind.Heading => 15u,
            HubRowKind.Note => 12u,
            _ => 14u,
        };

        // A note is prose, not a list entry: it is allowed to use both lines and it must wrap
        // rather than ellipsise, because the whole point of a note ("turn Quest Helper on to be
        // guided anywhere from here") is the part at the end of the sentence.
        labelNode.TextFlags = itemData.Kind == HubRowKind.Note
            ? TextFlags.WordWrap | TextFlags.MultiLine
            : TextFlags.Ellipsis;

        Layout();
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        Layout();
    }

    /// <inheritdoc/>
    protected override void OnNavSelected()
    {
        // Deliberately NOT calling base: the base toggles IsSelected and re-raises OnClick, which
        // ListNode also raises for a mouse click AND (unless AllowMultipleSelection is set) for
        // every row its own scroll-follows-focus passes over. Routing controller activation
        // straight to the row's action here keeps a held d-pad from firing everything it scrolls by.
        ItemData?.Activate?.Invoke();
    }

    private static Vector4 DefaultColor(HubRowKind kind) => kind switch
    {
        HubRowKind.Heading => GameColors.Heading,
        HubRowKind.Note => GameColors.Dimmed,
        _ => GameColors.ListText,
    };

    private void Layout()
    {
        var inner = Math.Max(Width - (Padding * 2f), 0f);
        var lineHeight = Math.Max((Height - LineGap) / 2f, 1f);

        switch (kind)
        {
            case HubRowKind.Note:
                // Both lines, one wrapping block.
                labelNode.Position = new Vector2(Padding, 2f);
                labelNode.Size = new Vector2(inner, Math.Max(Height - 4f, 1f));
                trailingNode.IsVisible = false;
                descriptionNode.IsVisible = false;
                return;

            case HubRowKind.Heading:
                // One line, centred in the row's height — a heading has no second line to balance
                // against, and floating it at the top would leave it orphaned above a gap.
                labelNode.Position = new Vector2(Padding, (Height - lineHeight) / 2f);
                labelNode.Size = new Vector2(Math.Max(inner - TrailingWidth - Padding, 0f), lineHeight);
                trailingNode.IsVisible = true;
                trailingNode.Position = new Vector2(Padding + inner - TrailingWidth, (Height - lineHeight) / 2f);
                trailingNode.Size = new Vector2(TrailingWidth, lineHeight);
                descriptionNode.IsVisible = false;
                return;

            default:
                labelNode.Position = new Vector2(Padding, 2f);
                labelNode.Size = new Vector2(Math.Max(inner - TrailingWidth - Padding, 0f), lineHeight);

                trailingNode.IsVisible = true;
                trailingNode.Position = new Vector2(Padding + inner - TrailingWidth, 3f);
                trailingNode.Size = new Vector2(TrailingWidth, lineHeight);

                descriptionNode.IsVisible = true;
                descriptionNode.Position = new Vector2(Padding, lineHeight + LineGap - 2f);
                descriptionNode.Size = new Vector2(inner, lineHeight);
                return;
        }
    }
}
