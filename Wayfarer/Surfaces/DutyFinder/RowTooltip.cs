using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Words the game shows when the player rests on one of our marks.
///
/// <para>The game is told to attach a tooltip to a node and keeps a pointer to the words rather
/// than a copy of them, so the words have to outlive the telling. They are held here, off the
/// managed heap where nothing moves them, and freed only once the game has been told to let the
/// node go.</para></summary>
internal sealed unsafe class RowTooltip : IDisposable
{
    private readonly nint words;
    private readonly nint node;

    private RowTooltip(nint node, nint words)
    {
        this.node = node;
        this.words = words;
    }

    /// <summary>Attaches words to a node, or nothing when there are no words to attach or no game
    /// to attach them to.</summary>
    /// <param name="node">The node the player will rest on.</param>
    /// <param name="addonId">The window the node belongs to, which is how the game files it.</param>
    /// <param name="text">What to say.</param>
    public static RowTooltip? Attach(AtkResNode* node, ushort addonId, string text)
    {
        var stage = AtkStage.Instance();
        if (node == null || stage == null || string.IsNullOrEmpty(text))
        {
            return null;
        }

        // The node has to be one the window hit-tests, or the game is never asked about it.
        node->NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;

        var held = Marshal.StringToHGlobalAnsi(text);
        var args = default(AtkTooltipManager.AtkTooltipArgs);
        args.TextArgs.Text = (byte*)held;
        stage->TooltipManager.AttachTooltip(AtkTooltipType.Text, addonId, node, &args);
        return new RowTooltip((nint)node, held);
    }

    /// <summary>Tells the game to let the node go, and only then frees the words.</summary>
    public void Dispose()
    {
        var stage = AtkStage.Instance();
        if (stage != null && node != 0)
        {
            stage->TooltipManager.DetachTooltip((AtkResNode*)node);
        }

        Marshal.FreeHGlobal(words);
    }
}
