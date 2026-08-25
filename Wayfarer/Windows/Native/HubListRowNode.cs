using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The view for <see cref="HubListRow"/>. Built from plain <c>TextNode</c>s, which are
/// structurally incapable of holding cursor-navigation info — <c>ListItemWithFocusNav</c> is what
/// smuggles a zero-size component into the row so it can own a nav slot and turn a confirm press
/// into an activation. Confirm arrives as the <b>logical</b> <c>InputId.OK</c>, so the player's own
/// PadReverseConfirmCancel setting is honoured without us reading it.
///
/// <para><b>Shape.</b> Two lines and two columns, mirroring the Duty Finder's own tree-list row:
/// the name on line one with the level pinned to the right of it, and a dimmed qualifying line
/// underneath with the state pinned to the right of that. The previous row was a single 26px line
/// that gave the name ~590px it did not need and squeezed zone, level and state into a 132px
/// gutter, ellipsising all three — 90% of the row was blank and the 10% that carried meaning was
/// the part that got cut. On a TV at 200% HUD scale it was unreadable before it was
/// truncated.</para>
///
/// <para><b>Hierarchy.</b> Three registers, in the game's own faces: the name in Axis 14 at the
/// Duty Finder's own warm list colour, the level and the state in Axis 12 captions, and the
/// description in the dimmed Axis 12 the Journal gives its sub-row. The name is never dimmed to
/// carry a state — that put it in the same colour and the same weight as the line beneath it and
/// left the eye nothing to land on, which is what a list of two hundred locked entries looked
/// like.</para></summary>
internal sealed class HubListRowNode : ListItemWithFocusNav<HubListRow>, IListItemNode
{
    private readonly IconImageNode iconNode;
    private readonly TextNode labelNode;
    private readonly TextNode trailingNode;
    private readonly TextNode descriptionNode;
    private readonly TextNode statusNode;

    private HubRowKind kind = HubRowKind.Entry;
    private bool hasIcon;
    private bool hasStatus;

    public HubListRowNode()
    {
        iconNode = new IconImageNode
        {
            Size = new Vector2(GameMetrics.Row.IconSize, GameMetrics.Row.IconSize),
            FitTexture = true,
            IsVisible = false,
        };
        iconNode.AttachNode(this);

        labelNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.BodySize,
            LineSpacing = GameMetrics.Type.BodyLine,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.ListText,
        };
        labelNode.AttachNode(this);

        // Line one's caption: the level, pinned right.
        trailingNode = Secondary(AlignmentType.Right);
        trailingNode.AttachNode(this);

        // Line two: the description, which is prose and therefore the one line on the row that is
        // allowed to be cut. Ellipsis rather than a hand-rolled character budget — the game's own
        // text engine measures in the font that is actually being drawn, at the size it is actually
        // being drawn, which a character count cannot do and which is exactly how a "safe" budget
        // ends up cutting a word in half on one HUD scale and leaving a third of the row empty on
        // another.
        descriptionNode = Secondary(AlignmentType.Left);
        descriptionNode.AttachNode(this);

        // Line two's caption: the state's own column, under the level and against the same right
        // edge. Only ever filled when the state's shape could not be drawn — see
        // HubListRow.StatusWord.
        statusNode = Secondary(AlignmentType.Right);
        statusNode.IsVisible = false;
        statusNode.AttachNode(this);

        // The pointer's half of "cursor moves, detail updates". The controller's half is
        // OnNavHoverStart below, and both call the same method — one behaviour, not two that can
        // drift. SelectableNode has already registered MouseOver for its own highlight, and
        // AddEvent appends to the existing handler rather than replacing it.
        AddEvent(AtkEventType.MouseOver, PublishHover);
    }

    /// <summary>Row height, a per-type constant because the list virtualizes on it. The game's own
    /// Axis-14 row (24) stacked on its own Axis-12 row (24) — see
    /// <see cref="GameMetrics.Row.EntryHeight"/>. Section headings are shorter in the game but have
    /// to share this height, because the list can only virtualize on one.
    ///
    /// <para>Deliberately not a setting. A knob for row height is a design decision being deferred
    /// to the player, and the number is the game's, not ours.</para></summary>
    public static float ItemHeight => GameMetrics.Row.EntryHeight;

    /// <summary>Puts the game's cursor back on this row — what the journal page does when it closes,
    /// so a player returns to the entry they opened rather than to the top of the tab.
    ///
    /// <para>The row's own focus target is the zero-size component <c>ListItemWithFocusNav</c>
    /// smuggles in; the text nodes it is built from cannot hold focus. Exposed here because that
    /// node is <c>protected</c>, and the alternative — putting the cursor on the tab's action button
    /// instead — would lose the player's place in a list of several hundred rows.</para></summary>
    public void TakeFocus() => NavFocusNode.SetFocus();

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

        ApplyIcon(itemData.IconId);

        labelNode.String = itemData.Label;
        trailingNode.String = itemData.Detail;

        // The description is the description, whatever else the row has to say. The state's word
        // only appears when its shape could not be drawn, and it goes in its own column on the
        // right of line two — never in front of the sentence, which is what turned every row in a
        // list of locked entries into the same opening two words.
        descriptionNode.String = itemData.Description;

        hasStatus = itemData.Kind == HubRowKind.Entry
            && !hasIcon
            && itemData.StatusWord is { Length: > 0 };
        statusNode.String = hasStatus ? itemData.StatusWord : string.Empty;
        statusNode.TextColor = itemData.StatusColor ?? GameColors.Dimmed;

        labelNode.TextColor = itemData.LabelColor ?? DefaultColor(itemData.Kind);

        // A section header is set in the same Axis 14 as an entry — Journal's own header row does
        // exactly that and leans on the icon and the count beside it to do the separating, rather
        // than on a size the rest of the list does not use.
        labelNode.FontSize = itemData.Kind == HubRowKind.Note
            ? GameMetrics.Type.SecondarySize
            : GameMetrics.Type.BodySize;
        labelNode.LineSpacing = itemData.Kind == HubRowKind.Note
            ? GameMetrics.Type.SecondaryLine
            : GameMetrics.Type.BodyLine;

        // A note is prose, not a list entry: it is allowed to use both lines and it must wrap
        // rather than ellipsise, because the whole point of a note ("turn Quest Helper on to be
        // guided anywhere from here") is the part at the end of the sentence.
        //
        // A heading is the third case: the same Axis 14 as an entry, and set apart by the outline
        // instead. The game's panel headers are drawn white over the bronze edge in UIColor row 54
        // — that pairing is what makes a title read as a vanilla HUD header rather than as another
        // row — and this list has no section icon to lean on the way Journal's own header does.
        labelNode.TextFlags = itemData.Kind switch
        {
            HubRowKind.Note => TextFlags.WordWrap | TextFlags.MultiLine,
            HubRowKind.Heading => TextFlags.Edge | TextFlags.Ellipsis,
            _ => TextFlags.Ellipsis,
        };
        labelNode.TextOutlineColor = GameColors.HeadingEdge;

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

    /// <summary>The controller's half of "cursor moves, detail updates" — fired by
    /// <c>NavFocusNode</c> on every d-pad step onto this row, which is the callback pair the
    /// vendored toolkit already exposes and this node previously ignored.</summary>
    protected override void OnNavHoverStart()
    {
        base.OnNavHoverStart();
        PublishHover();
    }

    private static Vector4 DefaultColor(HubRowKind kind) => kind switch
    {
        HubRowKind.Heading => GameColors.Heading,
        HubRowKind.Note => GameColors.Dimmed,
        _ => GameColors.ListText,
    };

    /// <summary>The row's lower register: the dimmed Axis 12 the Journal's own sub-row is set in
    /// (<c>1022 #2</c>), which is what everything on line two and the caption beside the name are
    /// drawn in. One factory, so the three of them cannot drift into three sizes.</summary>
    private static TextNode Secondary(AlignmentType alignment) => new()
    {
        FontType = FontType.Axis,
        FontSize = GameMetrics.Type.SecondarySize,
        LineSpacing = GameMetrics.Type.SecondaryLine,
        AlignmentType = alignment,
        TextFlags = TextFlags.Ellipsis,
        TextColor = GameColors.Dimmed,
    };

    private static RowShape Shape(HubRowKind kind) => kind switch
    {
        HubRowKind.Heading => RowShape.Section,
        HubRowKind.Note => RowShape.Note,
        _ => RowShape.Entry,
    };

    private static void Place(TextNode node, ScreenRect rect)
    {
        node.IsVisible = !rect.IsEmpty;
        if (rect.IsEmpty)
        {
            return;
        }

        node.Position = new Vector2(rect.X, rect.Y);
        node.Size = new Vector2(rect.Width, rect.Height);
    }

    private void PublishHover()
    {
        if (ItemData is { } data)
        {
            data.Hover?.Invoke(data);
        }
    }

    private void ApplyIcon(uint iconId)
    {
        hasIcon = iconId != 0;
        iconNode.IsVisible = hasIcon;
        if (!hasIcon)
        {
            return;
        }

        iconNode.IconId = iconId;

        // The part rectangle has to match the size the icon is authored at, or the node samples past
        // the edge of the texture and draws a band of nothing. Prefer what the game says the loaded
        // texture is; fall back to the block the id belongs to when it cannot answer yet, and
        // correct itself on the next assignment if the seed was wrong.
        var actual = iconNode.ActualTextureSize;
        iconNode.TextureSize = actual.X > 0f && actual.Y > 0f ? actual : HubStatusIcons.SourceSize(iconId);
    }

    private void Layout()
    {
        // Every rectangle comes from RowLayout, which is measured against the game's own tree-list
        // rows and clipped to the row's own box — so nothing here can put a node outside it.
        var shape = Shape(kind);
        var blocks = RowLayout.Compose(
            shape, Width, Height, hasIcon && shape == RowShape.Entry, hasStatus && shape == RowShape.Entry);

        iconNode.IsVisible = !blocks.Icon.IsEmpty;
        if (!blocks.Icon.IsEmpty)
        {
            iconNode.Position = new Vector2(blocks.Icon.X, blocks.Icon.Y);
            iconNode.Size = new Vector2(blocks.Icon.Width, blocks.Icon.Height);
        }

        Place(labelNode, blocks.Label);
        Place(trailingNode, blocks.Trailing);
        Place(descriptionNode, blocks.Description);
        Place(statusNode, blocks.Status);
    }
}
