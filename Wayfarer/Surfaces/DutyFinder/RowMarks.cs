using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>What has been done to one row, and everything needed to undo it.
///
/// <para>A row is only ever changed to make room, and what was changed is written down as it is
/// done rather than worked out again later. The game's own parts are put back from this record;
/// ours are simply freed.</para></summary>
/// <param name="Marks">The nodes drawn on this row, which are ours to free.</param>
internal sealed unsafe record RowMarks(List<IconImageNode> Marks)
{
    /// <summary>The game's own parts this row had moved or resized, as they were before.</summary>
    public List<WasAt> Moved { get; } = [];

    /// <summary>The row's name and how wide it was before it gave up room, or null when it has
    /// not been asked to.</summary>
    public WasAt? Name { get; set; }

    /// <summary>Puts every part of the game's back the way it was found. Safe to call twice, and
    /// safe to call having changed nothing.</summary>
    public void Restore()
    {
        foreach (var was in Moved)
        {
            was.Restore();
        }

        Moved.Clear();
        Name?.Restore();
        Name = null;
    }

    /// <summary>Frees the nodes drawn on this row.</summary>
    public void Free()
    {
        foreach (var mark in Marks)
        {
            mark.Dispose();
        }

        Marks.Clear();
    }

    /// <summary>One of the game's parts as it stood before it was asked to move over.</summary>
    /// <param name="Node">The part itself, which lives as long as the row does.</param>
    /// <param name="X">Where it was.</param>
    /// <param name="Width">How wide it was.</param>
    /// <param name="Height">How tall it was.</param>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct WasAt(nint Node, float X, ushort Width, ushort Height)
    {
        /// <summary>Reads a part down as it stands now.</summary>
        public static WasAt Of(AtkResNode* node) => new((nint)node, node->X, node->Width, node->Height);

        /// <summary>Puts it back.</summary>
        public void Restore()
        {
            var node = (AtkResNode*)Node;
            if (node == null)
            {
                return;
            }

            node->SetXFloat(X);
            node->SetWidth(Width);
            node->SetHeight(Height);
        }
    }
}
