using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>One mark drawn on a row: the icon the player sees, and the patch of row that notices
/// the pointer resting on it.
///
/// <para>An icon is only drawn; it is never asked about. The game hit-tests a window through its
/// collision nodes, which is why each of its own row icons has one sitting over it, so a mark that
/// is to say anything about itself needs one too.</para></summary>
internal sealed unsafe class RowMark : IDisposable
{
    private readonly IconImageNode icon;
    private readonly CollisionNode touch;

    private RowTooltip? says;

    private RowMark(IconImageNode icon, CollisionNode touch)
    {
        this.icon = icon;
        this.touch = touch;
    }

    /// <summary>Hangs a new mark beside a row's name, or null when there is no name to hang it
    /// beside.</summary>
    public static RowMark? Beside(AtkTextNode* name)
    {
        if (name == null)
        {
            return null;
        }

        var drawn = new IconImageNode { FitTexture = true, IsVisible = false };
        drawn.AttachNode(name, NodePosition.AfterTarget);

        var felt = new CollisionNode { IsVisible = true };
        felt.AttachNode(name, NodePosition.AfterTarget);
        return new RowMark(drawn, felt);
    }

    /// <summary>Draws the mark, and puts the patch that notices the pointer exactly over it.</summary>
    /// <param name="iconId">What to draw.</param>
    /// <param name="at">Where the strip put it.</param>
    /// <param name="size">How big the strip made it.</param>
    public void Show(uint iconId, Vector2 at, Vector2 size)
    {
        icon.IconId = iconId;
        icon.Size = size;
        icon.Position = at;
        icon.IsVisible = true;

        touch.Size = size;
        touch.Position = at;
    }

    /// <summary>Hides the mark, for a row that no longer wants this many.</summary>
    public void Hide() => icon.IsVisible = false;

    /// <summary>Says what the mark means, replacing whatever it said before.</summary>
    /// <param name="addon">The window the row belongs to.</param>
    /// <param name="text">What the mark means.</param>
    public void Explain(AddonContentsFinder* addon, string text)
    {
        Forget();
        if (addon != null)
        {
            says = RowTooltip.Attach(&touch.Node->AtkResNode, addon->AtkUnitBase.Id, text);
        }
    }

    /// <summary>Takes back what was said about the mark, which the game must be told before the
    /// words behind it go.</summary>
    public void Forget()
    {
        says?.Dispose();
        says = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Forget();
        touch.Dispose();
        icon.Dispose();
    }
}
