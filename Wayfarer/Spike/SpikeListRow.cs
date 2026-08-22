using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace Wayfarer.Spike;

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. The row model for
/// <see cref="SpikeNavWindow"/>'s list. <see cref="Selections"/> counts every activation the list
/// reports (mouse click OR controller confirm) and <see cref="PadConfirms"/> counts only the ones
/// that arrived as <c>InputId.OK</c> through <c>NavFocusNode</c>, so the two paths can be told
/// apart on screen without a debugger.</summary>
internal sealed class SpikeRowModel
{
    public required int Number { get; init; }

    public required string Label { get; init; }

    public int Selections { get; set; }

    public int PadConfirms { get; set; }

    public string Display => (Selections, PadConfirms) switch
    {
        (0, 0) => $"{Number:00}   {Label}",
        _ => $"{Number:00}   {Label}      selected {Selections}  ·  pad confirm {PadConfirms}",
    };
}

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. A row made of a plain
/// <see cref="TextNode"/>, which is structurally incapable of holding cursor-navigation info;
/// <c>ListItemWithFocusNav</c> is what smuggles a zero-size component into the row so it can own a
/// nav slot. This is the exact shape the real checklist/hunting rows would take.</summary>
internal sealed class SpikeRowNode : ListItemWithFocusNav<SpikeRowModel>, IListItemNode
{
    private readonly TextNode labelNode;

    public SpikeRowNode()
    {
        labelNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 14,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = new Vector4(1f, 1f, 1f, 1f),
        };
        labelNode.AttachNode(this);
    }

    /// <summary>Row height is a per-type constant because <c>ListNode</c> virtualizes on it —
    /// 28px matches the game's own list rows closely enough for the spike.</summary>
    public static float ItemHeight => 28.0f;

    /// <summary>Raised only for a controller confirm (<c>InputId.OK</c>), never for a mouse click
    /// and never for the synthetic click <c>ListNode</c> fires while scroll-follows-focus moves the
    /// selection — which is what makes "confirm activates a row" provable rather than inferred.</summary>
    public Action<SpikeRowModel>? OnPadConfirmed { get; set; }

    /// <summary>Re-renders from the (mutable) model. <c>ListNode.Update()</c> calls this on every
    /// visible row, which is how an activation counter can change without rebuilding the list.</summary>
    public override void Update()
    {
        if (ItemData is { } data)
        {
            labelNode.String = data.Display;
        }
    }

    protected override void SetNodeData(SpikeRowModel itemData) => labelNode.String = itemData.Display;

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        labelNode.Position = new Vector2(8.0f, 5.0f);
        labelNode.Size = new Vector2(Math.Max(Width - 16.0f, 0.0f), Math.Max(Height - 8.0f, 0.0f));
    }

    protected override void OnNavSelected()
    {
        base.OnNavSelected();

        if (ItemData is { } data)
        {
            OnPadConfirmed?.Invoke(data);
        }
    }
}
