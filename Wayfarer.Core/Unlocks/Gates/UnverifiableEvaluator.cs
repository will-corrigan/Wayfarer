using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>A requirement that is known to exist and cannot be expressed as any other kind. Always
/// Indeterminate, carrying the curated sentence as the reason — the honest "we don't know", never
/// a silent pass.</summary>
public sealed class UnverifiableEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.Unverifiable;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        return GateResult.Unknown(
            node.Display is { Length: > 0 } d ? d : "has a requirement Wayfarer cannot read");
    }
}
