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
    private readonly bool wasFound;

    private RowTooltip(nint node, nint words, bool wasFound)
    {
        this.node = node;
        this.words = words;
        this.wasFound = wasFound;
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

        // The patch has to be one the pointer is actually tested against. A slot the game is not
        // using keeps its patch but hides it, and a hidden patch is never tested, so it is shown
        // for as long as we have something to say and hidden again afterwards.
        var showing = node->IsVisible();
        node->NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
        node->ToggleVisibility(true);

        var held = Marshal.StringToHGlobalAnsi(text);
        var args = default(AtkTooltipManager.AtkTooltipArgs);
        args.TextArgs.Text = (byte*)held;
        stage->TooltipManager.AttachTooltip(AtkTooltipType.Text, addonId, node, &args);
        return new RowTooltip((nint)node, held, showing);
    }

    /// <summary>Tells the game to let the node go, and only then frees the words.</summary>
    public void Dispose()
    {
        var stage = AtkStage.Instance();
        var patch = (AtkResNode*)node;
        if (stage != null && patch != null)
        {
            stage->TooltipManager.DetachTooltip(patch);
            patch->ToggleVisibility(wasFound);
        }

        Marshal.FreeHGlobal(words);
    }
}
