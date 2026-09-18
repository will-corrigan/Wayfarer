using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Listens to the guide's own plate for the pad's Down press while the plate has the
/// cursor, so the block can take the cursor itself. The plate is a component the game owns with
/// its own input code, which never hands the cursor to nodes it does not know; this is how we get
/// told anyway. Registered on the plate's node and on its focus node, since the game may deliver
/// input to either; unregistered on detach.</summary>
internal sealed unsafe class PlatePadListener : IDisposable
{
    private const uint NoEventParam = 0;
    private const bool NotGlobal = false;

    private readonly CustomEventListener listener;
    private readonly Action onDown;
    private readonly List<nint> registeredOn = [];

    public PlatePadListener(Action onDown)
    {
        this.onDown = onDown;
        listener = new CustomEventListener(Receive);
    }

    public void Attach(AtkComponentNode* plate)
    {
        Register((AtkResNode*)plate);
        var focus = plate->Component->GetFocusNode();
        if (focus != null && focus != (AtkResNode*)plate)
        {
            Register(focus);
        }
    }

    public void Detach()
    {
        foreach (var address in registeredOn)
        {
            ((AtkResNode*)address)->AtkEventManager.UnregisterEvent(AtkEventType.InputReceived, NoEventParam, listener, NotGlobal);
        }

        registeredOn.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Detach();
        listener.Dispose();
    }

    private void Register(AtkResNode* node)
    {
        node->AtkEventManager.RegisterEvent(AtkEventType.InputReceived, NoEventParam, node, (AtkEventTarget*)node, listener, NotGlobal);
        registeredOn.Add((nint)node);
    }

    private void Receive(AtkEventListener* self, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        if (type == AtkEventType.InputReceived
            && data->InputData.State == InputState.Down
            && (InputId)data->InputData.InputId == InputId.DOWN)
        {
            onDown();
        }
    }
}
