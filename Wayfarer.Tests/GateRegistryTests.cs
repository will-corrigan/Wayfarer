using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Tests;

/// <summary>The dispatcher, the two combinators, and the rule that makes the whole model safe to
/// ship: a requirement this build cannot check is an admission, never a pass.</summary>
public class GateRegistryTests
{
    /// <summary>The single most important behaviour in the gate model. A catalogue written for a
    /// newer plugin — or edited by hand — names kinds this build has never heard of, and the
    /// difference between degrading to "we can't check this" and degrading to "go and get it" is
    /// the difference between a shrug and a wasted trip across a zone.</summary>
    [Fact]
    public void UnknownGateKind_IsRequirementsUnknown_NeverAvailable()
    {
        var result = GateEvaluatorRegistry.Standard.Evaluate(Gates.Node("notAThing"), Gates.Ctx(100).Live);

        Assert.Equal(GateOutcome.Indeterminate, result.Outcome);
        Assert.Equal(UnlockStatus.RequirementsUnknown, result.Status);
        Assert.Contains("notAThing", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownGateKind_OnAnEntry_IsRequirementsUnknown()
    {
        var all = new List<ResolvedUnlock>
        {
            new()
            {
                Def = new UnlockDefinition
                {
                    Unlock = "Something From The Future",
                    Type = "system",
                    Requires = new UnlockRequirement { Gates = [Gates.Node("notAThing")] },
                },
            },
        };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(100));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);
    }

    [Fact]
    public void Registry_RejectsDuplicateKinds()
    {
        var duplicate = Assert.Throws<ArgumentException>(
            () => new GateEvaluatorRegistry([new MountOwnedEvaluator(), new MountOwnedEvaluator()]));

        Assert.Contains(GateKinds.MountOwned, duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Standard_ImplementsEveryDeclaredKind()
    {
        Assert.Equal(
            [.. GateKinds.All.Order(StringComparer.Ordinal)],
            GateEvaluatorRegistry.Standard.Kinds.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    /// <summary>Unknown dominates blocked. A set with one missing mount and one unreadable rank is
    /// not "you need that mount" — it is "we don't know", because the unreadable half might be the
    /// only thing really standing in the way.</summary>
    [Fact]
    public void AllOf_OneBlockedOneUnknown_IsRequirementsUnknown()
    {
        var node = Gates.Node(
            GateKinds.AllOf,
            display: "two things",
            children:
            [
                Gates.Node(GateKinds.MountOwned, [76u], display: "rose lanner"),
                Gates.Node(GateKinds.AchievementComplete, [2867u], display: "an achievement"),
            ]);

        Assert.Equal(GateOutcome.Indeterminate, Evaluate(node, Gates.Ctx(100)).Outcome);
    }

    [Fact]
    public void AllOf_SeveralBlocked_NamesHowManyAreLeft()
    {
        var node = Gates.Node(
            GateKinds.AllOf,
            display: "all seven lanners",
            children:
            [
                Gates.Node(GateKinds.MountOwned, [76u], display: "rose lanner"),
                Gates.Node(GateKinds.MountOwned, [75u], display: "white lanner"),
            ]);

        var result = Evaluate(node, Gates.Ctx(100));

        Assert.Equal(GateOutcome.Blocked, result.Outcome);
        Assert.Contains("all seven lanners", result.Reason, StringComparison.Ordinal);
        Assert.Contains("rose lanner", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AllOf_EveryChildSatisfied_IsSatisfied()
    {
        var node = Gates.Node(
            GateKinds.AllOf,
            children:
            [
                Gates.Node(GateKinds.MountOwned, [76u], display: "rose lanner"),
                Gates.Node(GateKinds.CharacterLevelAtLeast, amount: 60),
            ]);

        Assert.Equal(
            GateOutcome.Satisfied,
            Evaluate(node, Gates.Ctx(60, isMountUnlocked: _ => true)).Outcome);
    }

    [Fact]
    public void AnyOf_OneSatisfiedOneUnknown_IsSatisfied()
    {
        var node = Gates.Node(
            GateKinds.AnyOf,
            children:
            [
                Gates.Node(GateKinds.AchievementComplete, [2867u], display: "an achievement"),
                Gates.Node(GateKinds.QuestComplete, [66216u], display: "a quest"),
            ]);

        Assert.Equal(
            GateOutcome.Satisfied,
            Evaluate(node, Gates.Ctx(100, isQuestComplete: _ => true)).Outcome);
    }

    [Fact]
    public void AnyOf_NoneSatisfiedOneUnknown_IsRequirementsUnknown()
    {
        var node = Gates.Node(
            GateKinds.AnyOf,
            children:
            [
                Gates.Node(GateKinds.AchievementComplete, [2867u], display: "an achievement"),
                Gates.Node(GateKinds.QuestComplete, [66216u], display: "a quest"),
            ]);

        Assert.Equal(GateOutcome.Indeterminate, Evaluate(node, Gates.Ctx(100)).Outcome);
    }

    [Fact]
    public void AnyOf_EveryChildBlocked_IsBlocked()
    {
        var node = Gates.Node(
            GateKinds.AnyOf,
            display: "ten relic quests",
            children:
            [
                Gates.Node(GateKinds.QuestComplete, [66655u], display: "one relic"),
                Gates.Node(GateKinds.QuestComplete, [66656u], display: "another relic"),
            ]);

        var result = Evaluate(node, Gates.Ctx(100));

        Assert.Equal(GateOutcome.Blocked, result.Outcome);
        Assert.Contains("ten relic quests", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnyOf_WithNoChildren_IsIndeterminate()
    {
        Assert.Equal(GateOutcome.Indeterminate, Evaluate(Gates.Node(GateKinds.AnyOf), Gates.Ctx(100)).Outcome);
    }

    /// <summary>Combinators nest through the same dispatcher, so a tree costs nothing extra.</summary>
    [Fact]
    public void Combinators_Nest()
    {
        var node = Gates.Node(
            GateKinds.AllOf,
            children:
            [
                Gates.Node(GateKinds.CharacterLevelAtLeast, amount: 70),
                Gates.Node(
                    GateKinds.AnyOf,
                    children:
                    [
                        Gates.Node(GateKinds.MountOwned, [76u], display: "rose lanner"),
                        Gates.Node(GateKinds.MinionOwned, [67u], display: "a minion"),
                    ]),
            ]);

        Assert.Equal(
            GateOutcome.Satisfied,
            Evaluate(node, Gates.Ctx(70, isMinionUnlocked: _ => true)).Outcome);
        Assert.Equal(GateOutcome.Blocked, Evaluate(node, Gates.Ctx(70)).Outcome);
    }

    private static GateResult Evaluate(GateNode node, UnlockGateContext ctx) =>
        GateEvaluatorRegistry.Standard.Evaluate(node, ctx.Live);
}
