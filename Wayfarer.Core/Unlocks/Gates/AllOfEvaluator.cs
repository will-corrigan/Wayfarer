using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Every child must be satisfied.</summary>
public sealed class AllOfEvaluator(GateEvaluatorRegistry registry) : IGateEvaluator
{
    public string Kind => GateKinds.AllOf;

    /// <summary>Blocked beats Indeterminate only when nothing is unknown: a set with one missing
    /// mount and one unreadable rank is not "you need that mount", it is "we don't know". Missing
    /// children are all collected — telling a player the first of seven is its own small lie.</summary>
    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        var blocked = new List<GateResult>();
        GateResult? unknown = null;
        foreach (var child in node.Children)
        {
            var result = registry.Evaluate(child, state);
            if (result.Outcome == GateOutcome.Blocked)
            {
                blocked.Add(result);
            }
            else if (result.Outcome == GateOutcome.Indeterminate)
            {
                unknown ??= result;
            }
        }

        if (unknown is { } u)
        {
            return u;
        }

        return blocked.Count == 0
            ? GateResult.Ok()
            : GateResult.Blocked(blocked[0].Status, Summarise(node, blocked));
    }

    private static string Summarise(GateNode node, List<GateResult> blocked)
    {
        var first = blocked[0].Reason ?? node.Describe();
        if (blocked.Count == 1)
        {
            return first;
        }

        var whole = node.Display is { Length: > 0 } d ? d : "a set";
        return $"needs {blocked.Count} more of {whole} — next: {first}";
    }
}
