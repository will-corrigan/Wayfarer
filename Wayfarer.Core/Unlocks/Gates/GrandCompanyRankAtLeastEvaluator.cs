using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Grand Company membership and rank. <c>ids</c> is optional: with a company id the gate
/// also demands that company, without one it accepts whichever the player joined.</summary>
public sealed class GrandCompanyRankAtLeastEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.GrandCompanyRankAtLeast;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count > 1 || node.Amount <= 0)
        {
            return GateResult.Unknown("malformed Grand Company requirement");
        }

        var company = state.Character.GrandCompany;
        if (company == 0)
        {
            return GateResult.Blocked(UnlockStatus.GrandCompanyLocked, "needs a Grand Company");
        }

        if (node.Ids.Count == 1 && node.Ids[0] != company)
        {
            return GateResult.Blocked(
                UnlockStatus.GrandCompanyLocked,
                node.Display is { Length: > 0 } d ? $"needs {d} membership" : "needs a different Grand Company");
        }

        return state.Character.GrandCompanyRank >= node.Amount
            ? GateResult.Ok()
            : GateResult.Blocked(
                UnlockStatus.GrandCompanyLocked,
                $"needs Grand Company rank {node.Amount.ToString(CultureInfo.InvariantCulture)}");
    }
}
