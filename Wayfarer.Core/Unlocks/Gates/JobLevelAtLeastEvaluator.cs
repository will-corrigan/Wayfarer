using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>One named job at a level, whatever job the player happens to be on. The reader always
/// asks for the real level rather than the level-synced one, so standing in a synced duty does not
/// close a gate the character actually meets.</summary>
public sealed class JobLevelAtLeastEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.JobLevelAtLeast;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1 || node.Amount <= 0)
        {
            return GateResult.Unknown("malformed job requirement");
        }

        return state.Character.ClassJobLevel(node.Ids[0]) >= node.Amount
            ? GateResult.Ok()
            : GateResult.Blocked(
                UnlockStatus.LevelLocked,
                $"needs {JobGateText.Describe(node.Display ?? "a specific job", [], node.Amount)}");
    }
}
