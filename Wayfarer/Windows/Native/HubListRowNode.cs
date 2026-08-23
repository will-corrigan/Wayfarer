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
/// <para><b>Shape.</b> Two lines, mirroring the Duty Finder's own tree-list row: name on line one
/// with a short right-hand caption, and a dimmed qualifying line underneath. The previous row was a
/// single 26px line that gave the name ~590px it did not need and squeezed zone, level and state
/// into a 132px gutter, ellipsising all three — 90% of the row was blank and the 10% that carried
/// meaning was the part that got cut. On a TV at 200% HUD scale it was unreadable before it was
/// truncated.</para></summary>
internal sealed class HubListRowNode : ListItemWithFocusNav<HubListRow>, IListItemNode
{
    private readonly IconImageNode iconNode;
    private readonly TextNode labelNode;
    private readonly TextNode trailingNode;
    private readonly TextNode descriptionNode;

    private HubRowKind kind = HubRowKind.Entry;
    private bool hasIcon;

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

        trailingNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
            AlignmentType = AlignmentType.Right,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        trailingNode.AttachNode(this);

        descriptionNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
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

    /// <inheritdoc/>
    public override void Update()
    {
        if (ItemData is { } data)
        {
            trailingNode.String = data.Detail;
            descriptionNode.String = !hasIcon && data.StatusWord is { Length: > 0 } word
                ? Prefixed(word, data.Description)
                : data.Description;
        }
    }

    /// <inheritdoc/>
    protected override void SetNodeData(HubListRow itemData)
    {
        kind = itemData.Kind;

        ApplyIcon(itemData.IconId);

        labelNode.String = itemData.Label;
        trailingNode.String = itemData.Detail;

        // The state's word only appears when its shape could not be drawn — and it goes on line
        // two, which has room for a word, rather than back into the gutter that could not hold it.
        descriptionNode.String = !hasIcon && itemData.StatusWord is { Length: > 0 } word
            ? Prefixed(word, itemData.Description)
            : itemData.Description;

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

    private static string Prefixed(string word, string description) =>
        description.Length == 0 ? word : $"{word} — {description}";

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
        var blocks = RowLayout.Compose(shape, Width, Height, hasIcon && shape == RowShape.Entry);

        iconNode.IsVisible = !blocks.Icon.IsEmpty;
        if (!blocks.Icon.IsEmpty)
        {
            iconNode.Position = new Vector2(blocks.Icon.X, blocks.Icon.Y);
            iconNode.Size = new Vector2(blocks.Icon.Width, blocks.Icon.Height);
        }

        Place(labelNode, blocks.Label);
        Place(trailingNode, blocks.Trailing);
        Place(descriptionNode, blocks.Description);
    }
}
