using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>One of the game's own parts as it stood before we asked it to move over, hide, or
/// change size. Written down as it is done, so putting it back is a matter of record.</summary>
/// <param name="Node">The part itself, which lives as long as the row does.</param>
/// <param name="X">How far across the row it stood.</param>
/// <param name="Y">How far down the row it stood.</param>
/// <param name="Width">How wide it was.</param>
/// <param name="Height">How tall it was.</param>
/// <param name="Shown">Whether it was being drawn.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly unsafe record struct RowMoved(nint Node, float X, float Y, ushort Width, ushort Height, bool Shown)
{
    /// <summary>Reads a part down as it stands now.</summary>
    public static RowMoved Of(AtkResNode* node) =>
        new((nint)node, node->X, node->Y, node->Width, node->Height, node->IsVisible());

    /// <summary>Puts it back.</summary>
    public void Restore()
    {
        var node = (AtkResNode*)Node;
        if (node == null)
        {
            return;
        }

        node->SetXFloat(X);
        node->SetYFloat(Y);
        node->SetWidth(Width);
        node->SetHeight(Height);
        node->ToggleVisibility(Shown);
    }
}
