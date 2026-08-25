using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>An achievement the player must have earned. The client holds no achievement data until
/// it has been fetched from the server, and a not-yet-fetched table reads as "you have earned
/// nothing" — so the reader distinguishes the two and this gate says so.</summary>
public sealed class AchievementCompleteEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.AchievementComplete;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1)
        {
            return GateResult.Unknown("malformed achievement requirement");
        }

        if (!state.Progress.TryAchievementComplete(node.Ids[0], out var complete))
        {
            return GateResult.Unknown("your achievements haven't loaded yet");
        }

        return complete
            ? GateResult.Ok()
            : GateResult.Blocked(UnlockStatus.CollectionLocked, $"needs the achievement {node.Describe()}");
    }
}
