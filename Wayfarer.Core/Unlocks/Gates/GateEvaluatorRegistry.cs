using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Dispatch for the whole gate model: a dictionary lookup on a string that came out of a
/// data file. There is no switch on entry identity here and there must never be one.</summary>
public sealed class GateEvaluatorRegistry
{
    private readonly Dictionary<string, IGateEvaluator> byKind = [];

    /// <summary>Initializes a new instance of the <see cref="GateEvaluatorRegistry"/> class over
    /// the kinds this build implements. Two evaluators claiming the same kind is a composition
    /// bug, not a precedence rule, so it throws rather than silently picking one.</summary>
    /// <param name="evaluators">One evaluator per kind.</param>
    public GateEvaluatorRegistry(IEnumerable<IGateEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        foreach (var evaluator in evaluators)
        {
            if (!byKind.TryAdd(evaluator.Kind, evaluator))
            {
                throw new ArgumentException(
                    $"two evaluators claim the gate kind '{evaluator.Kind}'", nameof(evaluators));
            }
        }
    }

    /// <summary>The registry every ordinary caller wants: all eighteen shipped kinds. Evaluators
    /// are stateless, so one instance serves the whole process.</summary>
    public static GateEvaluatorRegistry Standard { get; } = BuildStandard();

    /// <summary>Every kind this registry can answer, for the dataset test that keeps the data and
    /// the code from drifting apart.</summary>
    public IReadOnlyCollection<string> Kinds => byKind.Keys;

    /// <summary>A kind this build does not implement is the forward-compatibility case: a
    /// catalogue shipped with a newer plugin, or hand-edited. It degrades to Indeterminate, which
    /// becomes <see cref="UnlockStatus.RequirementsUnknown"/>, which is visibly "we don't know"
    /// rather than invisibly "go get it". This is the single most important line in the file.</summary>
    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        return byKind.TryGetValue(node.Kind, out var evaluator)
            ? evaluator.Evaluate(node, state)
            : GateResult.Unknown($"needs something this version of Wayfarer can't check ('{node.Kind}')");
    }

    /// <summary>Every node in the list, AND-ed — the implicit root of a <c>requires</c> block.
    /// Shares <see cref="AllOfEvaluator"/>'s rule that a single unknown makes the whole answer
    /// unknown, because "you are missing that mount" is a lie when a second gate could not be
    /// read at all.</summary>
    public GateResult EvaluateAll(IReadOnlyList<GateNode> nodes, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        GateResult? blocked = null;
        for (var i = 0; i < nodes.Count; i++)
        {
            var result = Evaluate(nodes[i], state);
            if (result.Outcome == GateOutcome.Indeterminate)
            {
                return result;
            }

            if (result.Outcome == GateOutcome.Blocked)
            {
                blocked ??= result;
            }
        }

        return blocked ?? GateResult.Ok();
    }

    private static GateEvaluatorRegistry BuildStandard()
    {
        // Two passes because the combinators need the registry they live in: build the leaves,
        // then hand the finished registry to the combinators that recurse through it.
        var leaves = new List<IGateEvaluator>
        {
            new QuestCompleteEvaluator(),
            new QuestAnyOfEvaluator(),
            new DutyUnlockedEvaluator(),
            new DutyCompleteEvaluator(),
            new MountOwnedEvaluator(),
            new MinionOwnedEvaluator(),
            new ItemHeldEvaluator(),
            new CharacterLevelAtLeastEvaluator(),
            new JobLevelAtLeastEvaluator(),
            new TribeRankAtLeastEvaluator(),
            new GrandCompanyRankAtLeastEvaluator(),
            new AchievementCompleteEvaluator(),
            new AetherCurrentsCompleteEvaluator(),
            new SharedFateRankAtLeastEvaluator(),
            new ZoneProgressAtLeastEvaluator(),
            new UnverifiableEvaluator(),
        };

        var registry = new GateEvaluatorRegistry(leaves);
        registry.byKind.Add(GateKinds.AllOf, new AllOfEvaluator(registry));
        registry.byKind.Add(GateKinds.AnyOf, new AnyOfEvaluator(registry));
        return registry;
    }
}
