using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>The duty is open to this character — the "have you taken the unlock" question the
/// catalogue used to answer with a shrug. The client keeps a bit per duty and it is exactly this.</summary>
public sealed class DutyUnlockedEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.DutyUnlocked;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1 || DutyScope.Of(node) is not { } space)
        {
            return GateResult.Unknown("malformed duty requirement");
        }

        if (!state.Content.TryDutyUnlocked(space, node.Ids[0], out var unlocked))
        {
            return GateResult.Unknown("your duty list isn't loaded yet");
        }

        return unlocked
            ? GateResult.Ok()
            : GateResult.Blocked(UnlockStatus.InstanceLocked, $"requires unlocking {node.Describe()}");
    }
}
