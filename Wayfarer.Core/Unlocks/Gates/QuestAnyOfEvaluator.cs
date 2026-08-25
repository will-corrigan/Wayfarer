using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Any one of several quests. The alternative-starting-city case, and the relic case:
/// a character is given exactly one of three rows, and asking only about the bound one told two
/// thirds of characters they had not started.</summary>
public sealed class QuestAnyOfEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.QuestAnyOf;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count == 0)
        {
            return GateResult.Unknown("malformed quest requirement");
        }

        foreach (var id in node.Ids)
        {
            if (state.Progress.IsQuestComplete(id))
            {
                return GateResult.Ok();
            }
        }

        return GateResult.Blocked(UnlockStatus.QuestLocked, $"needs quest '{node.Describe()}'");
    }
}
