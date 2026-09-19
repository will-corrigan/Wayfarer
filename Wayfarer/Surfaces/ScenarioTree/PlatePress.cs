using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Watches the guide for a press of its headline plate and offers it to whoever is
/// listening. When the press is taken, the game's own handling of it is skipped through Dalamud's
/// own <see cref="AddonArgs.PreventOriginal"/>, so the game never opens the page it would have
/// opened and there is nothing to undo afterwards.
///
/// <para>Dalamud's listener on the addon is used rather than an event registered on the plate
/// itself: a listener of ours on a game node makes the addon route its input through us, which
/// once cost the game its HUD Select. This only reads what the addon was already sent, and skips
/// the original for the one press it recognises.</para></summary>
internal sealed class PlatePress(IAddonLifecycle lifecycle) : IDisposable
{
    private Func<bool>? onPressed;

    /// <summary>Starts watching. <paramref name="handler"/> is asked on each press of the plate and
    /// says whether it took it, in which case the game does not see the press at all.</summary>
    public void Watch(Func<bool> handler)
    {
        onPressed = handler;
        lifecycle.RegisterListener(AddonEvent.PreReceiveEvent, AddonName, OnReceiveEvent);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, AddonName, OnReceiveEvent);
        onPressed = null;
    }

    /// <summary>Whether an event's target is the guide's headline plate: the plate's node, its
    /// component, or the collision node the plate is focused and clicked through.</summary>
    private static unsafe bool TargetsPlate(AtkUnitBase* addon, AtkEventTarget* target)
    {
        var plate = GuideNodes.Plate(addon);
        if (plate == null || plate->Component == null || target == null)
        {
            return false;
        }

        return target == (AtkEventTarget*)plate
            || target == (AtkEventTarget*)plate->Component
            || target == (AtkEventTarget*)plate->Component->GetFocusNode();
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

        var addon = (AtkUnitBase*)args.Addon.Address;
        var atkEvent = (AtkEvent*)received.AtkEvent;
        if (addon != null && atkEvent != null && TargetsPlate(addon, atkEvent->Target) && onPressed())
        {
            received.PreventOriginal();
        }
    }
}
