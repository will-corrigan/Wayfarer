using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Every aether current in one zone — the "can you fly here" question, asked of the
/// zone's own completion flag set rather than by counting currents one at a time.</summary>
public sealed class AetherCurrentsCompleteEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.AetherCurrentsComplete;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1)
        {
            return GateResult.Unknown("malformed aether current requirement");
        }

        if (!state.Progress.TryAetherCurrentZoneComplete(node.Ids[0], out var complete))
        {
            return GateResult.Unknown("your aether currents aren't loaded yet");
        }

        return complete
            ? GateResult.Ok()
            : GateResult.Blocked(
                UnlockStatus.CollectionLocked, $"needs every aether current in {node.Describe()}");
    }
}
