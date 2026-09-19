using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Watches the guide for a press of its headline plate and says so. Dalamud's own
/// listener is used rather than an event registered on the plate itself: a listener of ours on a
/// game node makes the addon route its input through us, which once cost the game its HUD Select.
/// This only reads what the addon already received.
///
/// <para>The press is watched after the game has handled it, so the game's own page opens first
/// and whoever is listening can then open the page they meant.</para></summary>
internal sealed class PlatePress(IAddonLifecycle lifecycle) : IDisposable
{
    private Action? onPressed;

    /// <summary>Starts watching, calling <paramref name="handler"/> on each press of the plate.</summary>
    public void Watch(Action handler)
    {
        onPressed = handler;
        lifecycle.RegisterListener(AddonEvent.PostReceiveEvent, AddonName, OnReceiveEvent);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, AddonName, OnReceiveEvent);
        onPressed = null;
    }

    /// <summary>Whether an event's target is the guide's headline plate: the plate's component, its
    /// node, or the collision node the plate is focused and clicked through.</summary>
    private static unsafe bool TargetsPlate(AtkUnitBase* addon, AtkEventTarget* target)
    {
        var plate = addon->GetComponentByNodeId(PlateNodeId);
        if (plate == null || target == null)
        {
            return false;
        }

        return target == (AtkEventTarget*)plate
            || target == (AtkEventTarget*)plate->OwnerNode
            || target == (AtkEventTarget*)plate->GetFocusNode();
    }

    private unsafe void OnReceiveEvent(AddonEvent type, AddonArgs args)
    {
        if (onPressed is null || args is not AddonReceiveEventArgs received)
        {
            return;
        }

        var kind = (AtkEventType)received.AtkEventType;
        if (kind is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))
        {
            return;
        }

        var atkEvent = (AtkEvent*)received.AtkEvent;
        if (atkEvent != null && TargetsPlate((AtkUnitBase*)args.Addon.Address, atkEvent->Target))
        {
            onPressed();
        }
    }
}
