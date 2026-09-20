using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>One icon in a row's strip, drawn by us whether it is ours or the game's.
///
/// <para>A mark of our own is an icon id, which the node loads into its own single part and draws
/// stretched to whatever size the strip gave it.</para>
///
/// <para>A rebuild of one of the game's is the very picture the game would have drawn: the node is
/// pointed at the game's own parts, and told to read them the way the game does. Which part of a
/// list to draw and how to fit it are separate things, and borrowing the first while keeping our
/// own second draws the whole sheet instead of the one picture. Everything borrowed is handed back
/// before the node is freed, so what the toolkit allocated is what the toolkit frees.</para>
/// </summary>
internal sealed unsafe class RowIcon : IDisposable
{
    private readonly IconImageNode node;
    private readonly nint parts;
    private readonly ImageNodeFlags fit;
    private readonly byte wrap;

    private RowIcon(IconImageNode node)
    {
        this.node = node;
        parts = node.Node == null ? 0 : (nint)node.Node->PartsList;
        fit = node.Node == null ? default : node.Node->Flags;
        wrap = node.Node == null ? (byte)0 : node.Node->WrapMode;
    }

    /// <summary>Hangs a new icon beside a row's name, or null when there is no name to hang it
    /// beside.</summary>
    public static RowIcon? Beside(AtkTextNode* name)
    {
        if (name == null)
        {
            return null;
        }

        var drawn = new IconImageNode { FitTexture = true, IsVisible = false };
        drawn.AttachNode(name, NodePosition.AfterTarget);
        return new RowIcon(drawn);
    }

    /// <summary>Draws one of our own marks.</summary>
    /// <param name="iconId">The icon to load.</param>
    /// <param name="at">Where it goes.</param>
    /// <param name="size">How big it is drawn.</param>
    public void Draw(uint iconId, Vector2 at, Vector2 size)
    {
        Reclaim();
        node.IconId = iconId;
        Place(at, size);
    }

    /// <summary>Draws the very picture one of the game's slots would have drawn.</summary>
    /// <param name="slot">The slot whose picture this is.</param>
    /// <param name="at">Where it goes.</param>
    /// <param name="size">How big it is drawn.</param>
    public void Draw(RowSlot slot, Vector2 at, Vector2 size)
    {
        if (node.Node == null || slot.Parts == null || slot.Picture == 0)
        {
            return;
        }

        var theirs = ((AtkResNode*)slot.Picture)->GetAsAtkImageNode();
        node.Node->PartsList = slot.Parts;
        node.Node->PartId = slot.Part;
        node.Node->Flags = theirs->Flags;
        node.Node->WrapMode = theirs->WrapMode;
        Place(at, size);
    }

    /// <summary>Hides the icon, for a row that wants fewer than it had.</summary>
    public void Hide() => node.IsVisible = false;

    /// <inheritdoc/>
    public void Dispose()
    {
        Reclaim();
        node.Dispose();
    }

    private void Place(Vector2 at, Vector2 size)
    {
        node.Size = size;
        node.Position = at;
        node.IsVisible = true;
    }

    /// <summary>Hands back everything borrowed, so nothing of the game's is freed with the node and
    /// nothing of ours is lost.</summary>
    private void Reclaim()
    {
        if (node.Node == null || parts == 0)
        {
            return;
        }

        node.Node->PartsList = (AtkUldPartsList*)parts;
        node.Node->PartId = 0;
        node.Node->Flags = fit;
        node.Node->WrapMode = wrap;
    }
}
