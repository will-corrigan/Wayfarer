using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Items the player must be carrying. <c>scope</c> is load-bearing rather than
/// decorative: key items are always resident, but an ordinary tradeable item may be sitting in a
/// retainer, a Free Company chest or house storage, none of which the client can enumerate while
/// closed. The reader says so rather than reporting a confident zero.</summary>
public sealed class ItemHeldEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.ItemHeld;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Ids.Count != 1 || ScopeOf(node) is not { } scope)
        {
            return GateResult.Unknown("malformed item requirement");
        }

        if (!state.Inventory.TryCount(node.Ids[0], scope, out var held))
        {
            return GateResult.Unknown("this could be in a retainer, which Wayfarer cannot see");
        }

        var needed = node.Amount > 0 ? node.Amount : 1;
        if (held >= needed)
        {
            return GateResult.Ok();
        }

        var what = needed > 1
            ? $"{node.Describe()} x{needed.ToString(CultureInfo.InvariantCulture)}"
            : node.Describe();
        return GateResult.Blocked(UnlockStatus.CollectionLocked, what);
    }

    private static ItemScope? ScopeOf(GateNode node) => node.Scope switch
    {
        null or "" or GateKinds.ScopeAny => ItemScope.Any,
        GateKinds.ScopeKeyItem => ItemScope.KeyItem,
        GateKinds.ScopeSaddlebag => ItemScope.Saddlebag,
        _ => null,
    };
}
