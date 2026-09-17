using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace Wayfarer.Windows.Native;

/// <summary>A control on a heads-up surface is two nodes that must agree about one rectangle: an
/// invisible box the pointer clicks, and a zero-sized component the game's controller cursor comes
/// to rest on. Built here, once, so every surface that offers a press grows both halves the same
/// way — a rectangle only one device can reach is the defect this exists to make unrepresentable.
/// </summary>
internal static class PressTargets
{
    /// <summary>Where a control's controller anchor sits inside that control's own rectangle: two
    /// units in from its left edge, and half its height down. The same offsets KamiToolKit's own
    /// navigable list rows use, so the game's cursor comes to rest beside a control here exactly as
    /// it does beside a row there.</summary>
    public const float NavAnchorInset = 2f;

    /// <summary>An invisible rectangle that turns a region into a click.
    ///
    /// <para>A <c>ResNode</c> draws nothing of its own, so the surface looks byte-for-byte the same
    /// with or without one — the only difference is a collision rectangle and the hand cursor over
    /// it. <c>MouseClick</c> is also the only event that adds <c>HasCollision</c>, so what swallows a
    /// world click is exactly this rectangle and nothing more.</para></summary>
    public static ResNode BuildHitBox(Action onClicked, NodeBase parent)
    {
        var box = new ResNode { IsVisible = false };
        box.AddEvent(AtkEventType.MouseClick, onClicked);
        box.ShowClickableCursor = true;
        box.AttachNode(parent);
        return box;
    }

    /// <summary>The controller's half of a hit box: a component the game's cursor can come to rest
    /// on, whose Confirm runs the same action the mouse's click does.
    ///
    /// <para><b>Zero size, and one flag off.</b> A component's collision node is built to fill and to
    /// answer the mouse; this one is sized to nothing and has <c>Fill</c> taken away, so it covers
    /// nothing. Null when there is no action to run: a surface that was given no callback grows no
    /// anchor, exactly as it grows no hit box.</para></summary>
    public static NavFocusNode? BuildNavAnchor(Action? onSelected, NodeBase parent)
    {
        if (onSelected is null)
        {
            return null;
        }

        var nav = new NavFocusNode
        {
            OnSelected = onSelected,
            Size = Vector2.Zero,
            IsVisible = false,
        };

        nav.CollisionNode.RemoveNodeFlags(NodeFlags.Fill);
        nav.AttachNode(parent);
        return nav;
    }

    /// <summary>Parks one anchor on one control: visible exactly when that control is, and two units
    /// in from its left edge at half its height, so the game's cursor sits beside the thing it is
    /// about to press rather than in its corner.</summary>
    public static void MirrorNav(NavFocusNode? nav, NodeBase? target)
    {
        if (nav is null || target is null)
        {
            return;
        }

        nav.IsVisible = target.IsVisible;
        if (!target.IsVisible)
        {
            return;
        }

        nav.Position = target.Position + new Vector2(NavAnchorInset, target.Height / 2f);
    }
}
