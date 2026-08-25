using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>At least one child must be satisfied — the shape of "a player does one relic".</summary>
public sealed class AnyOfEvaluator(GateEvaluatorRegistry registry) : IGateEvaluator
{
    public string Kind => GateKinds.AnyOf;

    /// <summary>The mirror of <see cref="AllOfEvaluator"/>: satisfied as soon as any child is,
    /// whatever the others said; Indeterminate when none is satisfied and any child could not be
    /// read; Blocked only when every child is Blocked, because only then is "you have done none
    /// of these" a thing we actually know.</summary>
    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Children.Count == 0)
        {
            return GateResult.Unknown("malformed any-of requirement");
        }

        GateResult? unknown = null;
        GateResult? blocked = null;
        foreach (var child in node.Children)
        {
            var result = registry.Evaluate(child, state);
            switch (result.Outcome)
            {
                case GateOutcome.Satisfied:
                    return GateResult.Ok();
                case GateOutcome.Indeterminate:
                    unknown ??= result;
                    break;
                default:
                    blocked ??= result;
                    break;
            }
        }

        if (unknown is { } u)
        {
            return u;
        }

        var b = blocked!.Value;
        return GateResult.Blocked(
            b.Status,
            node.Display is { Length: > 0 } d ? $"requires any one of {d}" : b.Reason ?? node.Describe());
    }
}
