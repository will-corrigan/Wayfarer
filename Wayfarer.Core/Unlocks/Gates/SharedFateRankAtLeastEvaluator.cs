using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Shared FATE rank in one zone. A different system from Allied Society reputation, and
/// the one the gemstone traders actually want — six Shadowbringers ZONES, not three societies.
///
/// <para>The rank arrives from the server rather than sitting in the client, and an unpopulated
/// slot reads as rank 0, which is also a legal rank. The reader tells them apart; this gate only
/// has to keep "not arrived" out of the "you are rank 0" answer.</para></summary>
public sealed class SharedFateRankAtLeastEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.SharedFateRankAtLeast;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1 || node.Amount <= 0)
        {
            return GateResult.Unknown("malformed Shared FATE requirement");
        }

        if (!state.Progress.TrySharedFateRank(node.Ids[0], out var rank))
        {
            return GateResult.Unknown("your Shared FATE progress hasn't loaded yet");
        }

        return rank >= node.Amount
            ? GateResult.Ok()
            : GateResult.Blocked(
                UnlockStatus.CollectionLocked,
                $"needs Shared FATE rank {node.Amount.ToString(CultureInfo.InvariantCulture)} in {node.Describe()}");
    }
}
