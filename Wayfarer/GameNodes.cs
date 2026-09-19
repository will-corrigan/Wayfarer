using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer;

/// <summary>How to reach a node inside one of the game's own windows, guarded. Every lookup is
/// done fresh from the window and every answer may be null, because a window's parts come and go
/// and a window itself is gone the moment the game closes it.
///
/// <para>This says nothing about which window or which node: a surface names its own parts, and
/// asks here for them. Nothing built from these answers may be held between frames.</para></summary>
internal static unsafe class GameNodes
{
    /// <summary>The component behind a node, or null when the window does not have it.</summary>
    public static AtkComponentBase* Component(AtkUnitBase* addon, uint nodeId) =>
        addon == null ? null : addon->GetComponentByNodeId(nodeId);

    /// <summary>The node itself as a component node, or null when it is not one or is not there.
    /// A game component node reports a type of its own component's id rather than a common one,
    /// so it can only be recognised by asking it, never by comparing its type.</summary>
    public static AtkComponentNode* ComponentNode(AtkUnitBase* addon, uint nodeId)
    {
        var node = addon == null ? null : addon->GetNodeById(nodeId);
        return node == null ? null : node->GetAsAtkComponentNode();
    }

    /// <summary>A text node of the window itself, or null.</summary>
    public static AtkTextNode* Text(AtkUnitBase* addon, uint nodeId)
    {
        var node = addon == null ? null : addon->GetNodeById(nodeId);
        return node == null ? null : node->GetAsAtkTextNode();
    }

    /// <summary>A text node inside a component, or null.</summary>
    public static AtkTextNode* Text(AtkComponentBase* component, uint textNodeId) =>
        component == null ? null : component->GetTextNodeById(textNodeId);

    /// <summary>Whether a node exists and is being drawn.</summary>
    public static bool Shown(AtkUnitBase* addon, uint nodeId)
    {
        var node = addon == null ? null : addon->GetNodeById(nodeId);
        return node != null && node->IsVisible();
    }

    /// <summary>Points a window's focus away from a node that is about to be freed, and moves the
    /// live cursor off it too. The game keeps raw pointers to whatever the pad is resting on, in
    /// the window and in the input manager, and follows them on the next input: a node freed while
    /// any of them names it is a crash waiting for the player to press a direction.</summary>
    /// <param name="addon">The window the node lives in.</param>
    /// <param name="node">The node being freed.</param>
    /// <param name="fallback">Where the focus should land instead, or null to leave it nowhere.</param>
    public static void HandFocusBack(AtkUnitBase* addon, AtkResNode* node, AtkResNode* fallback)
    {
        if (addon == null || node == null)
        {
            return;
        }

        if (addon->FocusNode == node)
        {
            addon->FocusNode = fallback;
        }

        if ((AtkResNode*)addon->ComponentFocusNode == node)
        {
            addon->ComponentFocusNode = null;
        }

        if (addon->CursorTarget == node)
        {
            addon->CursorTarget = null;
        }

        var stage = AtkStage.Instance();
        var input = stage == null ? null : stage->AtkInputManager;
        if (input != null && input->FocusedNode == node && fallback != null)
        {
            input->SetFocus(fallback, addon, 0);
        }
    }

    /// <summary>Whether a component has anything written in one of its text nodes. A part of a
    /// window can stay flagged visible with nothing in it, which on screen is a blank gap.</summary>
    public static bool HasWords(AtkComponentBase* component, uint textNodeId) =>
        Text(component, textNodeId) is var text && text != null && text->NodeText.Length > 0;
}
