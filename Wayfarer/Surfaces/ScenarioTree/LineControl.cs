using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The control of a pressable line: a guest in a game addon. While it lives the game may
/// point the addon's focus and cursor at its collision node, so when it is disposed it takes those
/// pointers back first. Nothing in the addon is left able to reach a freed node.</summary>
internal sealed unsafe class LineControl : NavFocusNode
{
    private nint host;
    private nint focusNode;
    private nint fallbackFocus;

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
            ReleaseHost();
        }

        base.Dispose(isNativeDestructor);
    }

    private void ReleaseHost()
    {
        var addon = (AtkUnitBase*)host;
        host = 0;
        if (addon == null || focusNode == 0)
        {
            return;
        }

        if ((nint)addon->FocusNode == focusNode)
        {
            addon->FocusNode = (AtkResNode*)fallbackFocus;
        }

        if ((nint)addon->ComponentFocusNode == focusNode)
        {
            addon->ComponentFocusNode = null;
        }

        if ((nint)addon->CursorTarget == focusNode)
        {
            addon->CursorTarget = null;
        }
    }
}
