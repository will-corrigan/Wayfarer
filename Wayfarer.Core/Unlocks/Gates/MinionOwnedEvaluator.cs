using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>A minion the player must already own.</summary>
public sealed class MinionOwnedEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.MinionOwned;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1)
        {
            return GateResult.Unknown("malformed minion requirement");
        }

        if (!state.Character.TryIsMinionUnlocked(node.Ids[0], out var owned))
        {
            return GateResult.Unknown("your collection isn't loaded yet");
        }

        return owned ? GateResult.Ok() : GateResult.Blocked(UnlockStatus.CollectionLocked, node.Describe());
    }
}
