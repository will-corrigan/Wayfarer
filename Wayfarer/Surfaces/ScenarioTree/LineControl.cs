using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The control of a pressable line: a guest in a game addon. While it shows, the game may
/// point the addon's focus and cursor at its collision node, so whenever it hides or is disposed it
/// hands those pointers back to the addon's own control first. Nothing in the addon is left aimed
/// at a hidden or freed node.</summary>
internal sealed unsafe class LineControl : NavFocusNode
{
    private nint host;
    private nint focusNode;
    private nint fallbackFocus;

    /// <inheritdoc/>
    public override bool IsVisible
    {
        get => base.IsVisible;
        set
        {
            if (!value && base.IsVisible)
            {
                HandBackFocus();
            }

            base.IsVisible = value;
        }
    }

    /// <summary>Names the addon the control lives in, and the node the addon's focus falls back to
    /// when the control goes. Only addresses are kept; nothing here is dereferenced later.</summary>
    public void GuestOf(AtkUnitBase* addon, AtkResNode* fallback)
    {
        host = (nint)addon;
        focusNode = (nint)CollisionNode.Node;
        fallbackFocus = (nint)fallback;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool isNativeDestructor)
    {
        // When the game itself is destroying the node, the addon is going down with it and must
        // not be written to. Only our own dispose hands the pointers back.
        if (!isNativeDestructor)
        {
            HandBackFocus();
            host = 0;
        }

        base.Dispose(isNativeDestructor);
    }

    /// <summary>Points the addon's focus at the fallback wherever it is aimed at this control, and
    /// moves the live cursor there too if it is resting on us.</summary>
    private void HandBackFocus()
    {
        var addon = (AtkUnitBase*)host;
        if (addon == null || focusNode == 0)
        {
            return;
        }

        var fallback = (AtkResNode*)fallbackFocus;
        if ((nint)addon->FocusNode == focusNode)
        {
            addon->FocusNode = fallback;
        }

        if ((nint)addon->ComponentFocusNode == focusNode)
        {
            addon->ComponentFocusNode = null;
        }

        if ((nint)addon->CursorTarget == focusNode)
        {
            addon->CursorTarget = null;
        }

        var input = AtkStage.Instance()->AtkInputManager;
        if ((nint)input->FocusedNode == focusNode && fallback != null)
        {
            input->SetFocus(fallback, addon, 0);
        }
    }
}
