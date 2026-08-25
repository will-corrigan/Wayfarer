using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Items the player must be carrying. <c>scope</c> is load-bearing rather than decorative:
/// it names which inventories are counted, and they are different questions.
///
/// <para><b>The limit of the answer, stated plainly.</b> <c>any</c> — which is also what an absent
/// scope means — counts what the client can enumerate: the bags, the armoury, the currency and
/// crystal tabs. A retainer, a Free Company chest and house storage are not enumerable while closed,
/// so for a tradeable item a zero means "not on you" rather than "not yours", and this evaluator
/// reports blocked either way. That is a deliberate trade rather than an oversight — see
/// <see cref="Live.IInventoryReader.TryCount"/>, which carries the reasoning and the live case behind
/// it — and it is the one place in the gate language where a definite answer is a shade stronger than
/// the read underneath it. Unknown is returned only when the scope has no reader at all, which today
/// means <c>saddlebag</c> on a host that wired none.</para></summary>
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
            return GateResult.Unknown("Wayfarer cannot read that inventory");
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
