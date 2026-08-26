using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>A title the player must already have earned.
///
/// <para><b>Three answers, not two.</b> The client holds no title list until something has asked
/// the server for one, and an unasked list reads as a character who has earned no titles at all —
/// which for a checklist of 870 of them would be the largest confident falsehood the plugin could
/// tell. So the reader distinguishes <i>not asked for</i>, <i>asked for and still coming</i>, and
/// <i>known</i>, and only the third produces a Blocked. The first two produce Indeterminate with
/// the reason that fits, which the calculator turns into
/// <see cref="UnlockStatus.RequirementsUnknown"/> — visibly "we don't know yet" rather than
/// invisibly "you have not got it".</para></summary>
public sealed class TitleUnlockedEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.TitleUnlocked;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1)
        {
            return GateResult.Unknown("malformed title requirement");
        }

        if (!state.Progress.TryTitleUnlocked(node.Ids[0], out var unlocked))
        {
            // Which of the two unknowns it is changes what the player should do about it, so the
            // sentence changes with it. Neither wording may read as "you have not earned this".
            return GateResult.Unknown(state.Progress.TitleData == TitleDataState.Pending
                ? "your titles are still on their way from the server"
                : "Wayfarer has not read your titles yet");
        }

        return unlocked
            ? GateResult.Ok()
            : GateResult.Blocked(UnlockStatus.CollectionLocked, $"needs the title {node.Describe()}");
    }
}
