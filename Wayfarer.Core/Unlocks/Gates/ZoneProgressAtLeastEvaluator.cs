using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Eureka elemental level or Bozja resistance rank. Both live on a content director that
/// only exists while the player is standing in the zone, so outside it this gate is honestly
/// Indeterminate — which is right where it can be right, and silent where it cannot.</summary>
public sealed class ZoneProgressAtLeastEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.ZoneProgressAtLeast;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Amount <= 0 || ScopeOf(node) is not { } kind)
        {
            return GateResult.Unknown("malformed zone progress requirement");
        }

        if (!state.Progress.TryZoneProgressRank(kind, out var rank))
        {
            return GateResult.Unknown($"Wayfarer can only read this inside {node.Describe()}");
        }

        return rank >= node.Amount
            ? GateResult.Ok()
            : GateResult.Blocked(
                UnlockStatus.CollectionLocked,
                $"needs {node.Describe()} {node.Amount.ToString(CultureInfo.InvariantCulture)}");
    }

    private static ZoneProgressKind? ScopeOf(GateNode node) => node.Scope switch
    {
        GateKinds.ScopeEureka => ZoneProgressKind.EurekaElemental,
        GateKinds.ScopeBozja => ZoneProgressKind.BozjaResistance,
        _ => null,
    };
}
