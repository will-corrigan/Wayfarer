using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>One quest, completed. The reader takes a full <c>uint</c> row id: the client's own
/// <c>IsQuestComplete</c> has a <c>ushort</c> overload that silently truncates one (67086 becomes
/// 1550) and answers about a different quest entirely, so the cast happens once, in the adapter.</summary>
public sealed class QuestCompleteEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.QuestComplete;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1)
        {
            return GateResult.Unknown("malformed quest requirement");
        }

        return state.Progress.IsQuestComplete(node.Ids[0])
            ? GateResult.Ok()
            : GateResult.Blocked(UnlockStatus.QuestLocked, $"needs quest '{node.Describe()}'");
    }
}
