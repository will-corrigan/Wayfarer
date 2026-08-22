using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace Wayfarer.Windows.Native;

/// <summary>The view for <see cref="HubListRow"/>. Built from plain <c>TextNode</c>s, which are
/// structurally incapable of holding cursor-navigation info — <c>ListItemWithFocusNav</c> is what
/// smuggles a zero-size component into the row so it can own a nav slot and turn a confirm press
/// into an activation. Confirm arrives as the <b>logical</b> <c>InputId.OK</c>, so the player's own
/// PadReverseConfirmCancel setting is honoured without us reading it.</summary>
internal sealed class HubListRowNode : ListItemWithFocusNav<HubListRow>, IListItemNode
{
    private const float Padding = 8f;
    private const float DetailWidth = 132f;

    private readonly TextNode labelNode;
    private readonly TextNode detailNode;

    public HubListRowNode()
    {
        labelNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 14,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Body,
        };
        labelNode.AttachNode(this);

        detailNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 12,
            AlignmentType = AlignmentType.Right,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        detailNode.AttachNode(this);
    }

    /// <summary>Row height, a per-type constant because the list virtualizes on it. 26px is close
    /// to the game's own list rows and keeps a full screen of rows inside the byte index space.</summary>
    public static float ItemHeight => 26f;

    /// <inheritdoc/>
    public override void Update()
    {
        if (ItemData is { } data)
        {
            detailNode.String = data.Detail;
        }
    }

    /// <inheritdoc/>
    protected override void SetNodeData(HubListRow itemData)
    {
        labelNode.String = itemData.Label;
        detailNode.String = itemData.Detail;
        labelNode.TextColor = itemData.LabelColor ?? DefaultColor(itemData.Kind);
        labelNode.FontSize = itemData.Kind == HubRowKind.Heading ? 15u : 14u;
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        var inner = Math.Max(Width - (Padding * 2f), 0f);
        var detail = Math.Min(DetailWidth, inner);
        labelNode.Position = new Vector2(Padding, 4f);
        labelNode.Size = new Vector2(Math.Max(inner - detail - Padding, 0f), Math.Max(Height - 6f, 0f));
        detailNode.Position = new Vector2(Padding + inner - detail, 5f);
        detailNode.Size = new Vector2(detail, Math.Max(Height - 8f, 0f));
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
}
