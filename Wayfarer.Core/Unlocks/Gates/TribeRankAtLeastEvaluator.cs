using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Allied Society reputation rank. Readable per tribe with no window open, so an entry
/// gated on a society's standing can be graded exactly rather than guessed at.</summary>
public sealed class TribeRankAtLeastEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.TribeRankAtLeast;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1 || node.Ids[0] > byte.MaxValue || node.Amount <= 0)
        {
            return GateResult.Unknown("malformed allied society requirement");
        }

        if (!state.Character.TryTribeRank((byte)node.Ids[0], out var rank))
        {
            return GateResult.Unknown("your reputation isn't loaded yet");
        }

        return rank >= node.Amount
            ? GateResult.Ok()
            : GateResult.Blocked(
                UnlockStatus.BeastTribeLocked,
                $"needs {node.Describe()} rank {node.Amount.ToString(CultureInfo.InvariantCulture)}");
    }
}
