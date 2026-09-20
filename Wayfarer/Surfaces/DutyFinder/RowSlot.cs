using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>One of the places a Duty Finder row keeps for an icon, as the game left it.
///
/// <para>A row is built with three of these and lights the ones it needs. Each holds the picture
/// the game would draw and the patch of row the pointer is tested against, and the two are separate
/// things: the picture is ours to take over, and the patch is the game's to keep, because what it
/// says when the pointer rests on it is decided by the window and cannot be read from here.</para>
/// </summary>
/// <param name="Frame">The slot itself.</param>
/// <param name="Picture">The image the game draws in it, or null when it draws none.</param>
/// <param name="Touch">The patch the pointer is tested against, or null when it has none.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly unsafe record struct RowSlot(nint Frame, nint Picture, nint Touch)
{
    /// <summary>Whether the game is drawing in this slot.</summary>
    public bool Lit => Picture != 0 && ((AtkResNode*)Picture)->IsVisible();

    /// <summary>The parts list the picture is drawn from, which is what another node needs to draw
    /// the very same picture.</summary>
    public AtkUldPartsList* Parts =>
        Picture == 0 ? null : ((AtkResNode*)Picture)->GetAsAtkImageNode()->PartsList;

    /// <summary>Which part of that list it draws.</summary>
    public ushort Part =>
        Picture == 0 ? (ushort)0 : ((AtkResNode*)Picture)->GetAsAtkImageNode()->PartId;
}
