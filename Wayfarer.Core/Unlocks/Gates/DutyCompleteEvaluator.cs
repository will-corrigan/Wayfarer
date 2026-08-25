using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>The duty has been CLEARED, not merely unlocked.</summary>
public sealed class DutyCompleteEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.DutyComplete;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1 || DutyScope.Of(node) is not { } space)
        {
            return GateResult.Unknown("malformed duty requirement");
        }

        if (!state.Content.TryDutyComplete(space, node.Ids[0], out var complete))
        {
            return GateResult.Unknown("your duty list isn't loaded yet");
        }

        return complete
            ? GateResult.Ok()
            : GateResult.Blocked(UnlockStatus.InstanceLocked, $"requires clearing {node.Describe()}");
    }
}
