using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Tests;

/// <summary>Every shipped gate kind, asked the same three questions: yes, no, and "we cannot tell".
///
/// <para>The third one is the reason this file is long. Two-valued readers are what let the
/// checklist say "you don't own that" to a player standing at the title screen, or "go and unlock
/// this" to one who unlocked it months ago — and every kind below has at least one state in which
/// the underlying read is not authoritative. A kind whose Indeterminate case is untested is a kind
/// whose Indeterminate case does not really exist.</para></summary>
public class GateEvaluatorTests
{
    private const uint AnyId = 42;

    private static readonly GateEvaluatorRegistry Registry = GateEvaluatorRegistry.Standard;

    [Fact]
    public void QuestComplete_Satisfied_Blocked_AndMalformed()
    {
        var node = Gates.Node(GateKinds.QuestComplete, [66216u], display: "The Company You Keep");
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(50, isQuestComplete: id => id == 66216)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(50)));
        Assert.Equal(
            GateOutcome.Indeterminate,
            Outcome(Gates.Node(GateKinds.QuestComplete), Gates.Ctx(50)));
    }

    [Fact]
    public void QuestAnyOf_AnyOneComplete_IsSatisfied()
    {
        var node = Gates.Node(GateKinds.QuestAnyOf, [66216u, 66217u, 66218u], display: "a Grand Company");
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(50, isQuestComplete: id => id == 66218)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(50)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(Gates.Node(GateKinds.QuestAnyOf), Gates.Ctx(50)));
    }

    [Fact]
    public void DutyUnlocked_ReadsTheInstanceSpace()
    {
        var node = Gates.Node(
            GateKinds.DutyUnlocked, [30066u], scope: GateKinds.ScopeInstance, display: "the Weapon's Refrain");
        Assert.Equal(
            GateOutcome.Satisfied,
            Outcome(node, Gates.Ctx(100, isInstanceContentUnlocked: id => id == 30066)));
        Assert.Equal(
            GateOutcome.Blocked,
            Outcome(node, Gates.Ctx(100, isInstanceContentUnlocked: _ => false)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(100, liveStateReady: false)));
    }

    /// <summary>The Diadem case, as a property rather than as an entry. A public-content id handed
    /// to the instance-content reader reads a different duty's bit and answers confidently, so a
    /// node whose scope names a space this build cannot read must say so.</summary>
    [Fact]
    public void DutyUnlocked_PublicSpace_NeverFallsBackToTheInstanceReader()
    {
        var node = Gates.Node(GateKinds.DutyUnlocked, [26u], scope: GateKinds.ScopePublic, display: "the Diadem");

        // The instance reader would say yes to anything; the public reader is not wired.
        Assert.Equal(
            GateOutcome.Indeterminate,
            Outcome(node, Gates.Ctx(100, isInstanceContentUnlocked: _ => true)));

        Assert.Equal(
            GateOutcome.Satisfied,
            Outcome(node, Gates.Ctx(100, isPublicContentUnlocked: id => id == 26)));
    }

    [Fact]
    public void DutyUnlocked_UnknownScope_IsIndeterminate()
    {
        var node = Gates.Node(GateKinds.DutyUnlocked, [26u], scope: "goldSaucer");
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(100, isInstanceContentUnlocked: _ => true)));
    }

    [Fact]
    public void DutyComplete_Satisfied_Blocked_Indeterminate()
    {
        var node = Gates.Node(
            GateKinds.DutyComplete, [30066u], scope: GateKinds.ScopeInstance, display: "Sigmascape V4.0 (Savage)");
        Assert.Equal(
            GateOutcome.Satisfied,
            Outcome(node, Gates.Ctx(100, isInstanceContentCompleted: id => id == 30066)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(100)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(100, liveStateReady: false)));
    }

    [Fact]
    public void MountOwned_Satisfied_Blocked_Indeterminate()
    {
        var node = Gates.Node(GateKinds.MountOwned, [76u], display: "rose lanner");
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(60, isMountUnlocked: id => id == 76)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(60)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(60, liveStateReady: false)));
    }

    [Fact]
    public void MinionOwned_Satisfied_Blocked_Indeterminate()
    {
        var node = Gates.Node(GateKinds.MinionOwned, [67u], display: "Minion of Light");
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(60, isMinionUnlocked: id => id == 67)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(60)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(60, liveStateReady: false)));
    }

    [Fact]
    public void ItemHeld_CountsAgainstAmount_AndScopesToTheContainer()
    {
        var keyItem = Gates.Node(
            GateKinds.ItemHeld, [2000123u], amount: 2, scope: GateKinds.ScopeKeyItem, display: "a token");
        Assert.Equal(GateOutcome.Satisfied, Outcome(keyItem, Gates.Ctx(60, getKeyItemCount: _ => 2)));
        Assert.Equal(GateOutcome.Blocked, Outcome(keyItem, Gates.Ctx(60, getKeyItemCount: _ => 1)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(keyItem, Gates.Ctx(60, liveStateReady: false)));

        // The saddlebag reader is not wired here, and the bags reader is not a substitute for it.
        var saddlebag = Gates.Node(
            GateKinds.ItemHeld, [12243u], scope: GateKinds.ScopeSaddlebag, display: "Timeworn Dragonskin Map");
        Assert.Equal(GateOutcome.Indeterminate, Outcome(saddlebag, Gates.Ctx(60, getOwnedItemCount: _ => 5)));
        Assert.Equal(GateOutcome.Satisfied, Outcome(saddlebag, Gates.Ctx(60, getSaddlebagItemCount: _ => 1)));

        // 'any', and the absent scope that means the same thing, are the confident path: they answer
        // from what the client can enumerate, and a zero is reported as blocked rather than as
        // "we cannot tell". Pinned because it is the one definite answer in the gate language that
        // is a shade stronger than the read underneath it — a tradeable item may be in a retainer —
        // and it must be a decision somebody made, not something that drifted. See
        // IInventoryReader.TryCount.
        foreach (var scope in (string?[])[GateKinds.ScopeAny, null])
        {
            var bags = Gates.Node(GateKinds.ItemHeld, [12243u], scope: scope, display: "Timeworn Dragonskin Map");
            Assert.Equal(GateOutcome.Satisfied, Outcome(bags, Gates.Ctx(60, getOwnedItemCount: _ => 1)));
            Assert.Equal(GateOutcome.Blocked, Outcome(bags, Gates.Ctx(60, getOwnedItemCount: _ => 0)));
            Assert.Equal(GateOutcome.Indeterminate, Outcome(bags, Gates.Ctx(60, liveStateReady: false)));
        }
    }

    [Fact]
    public void CharacterLevelAtLeast_Satisfied_Blocked_AndMalformed()
    {
        var node = Gates.Node(GateKinds.CharacterLevelAtLeast, amount: 80);
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(80)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(79)));
        Assert.Equal(
            GateOutcome.Indeterminate,
            Outcome(Gates.Node(GateKinds.CharacterLevelAtLeast), Gates.Ctx(80)));
    }

    [Fact]
    public void JobLevelAtLeast_AsksAboutThatJob_NotTheActiveOne()
    {
        var node = Gates.Node(GateKinds.JobLevelAtLeast, [18u], amount: 30, display: "Fisher");
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(1, getClassJobLevel: id => id == 18 ? 30 : 0)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(100)));
        Assert.Equal(
            GateOutcome.Indeterminate,
            Outcome(Gates.Node(GateKinds.JobLevelAtLeast, [18u]), Gates.Ctx(100)));
    }

    [Fact]
    public void TribeRankAtLeast_Satisfied_Blocked_Indeterminate()
    {
        var node = Gates.Node(GateKinds.TribeRankAtLeast, [13u], amount: 3, display: "Qitari");
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(80, getBeastTribeRank: id => id == 13 ? (byte)3 : (byte)0)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(80, getBeastTribeRank: _ => 2)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(80, liveStateReady: false)));
    }

    [Fact]
    public void GrandCompanyRankAtLeast_ChecksMembershipThenRank()
    {
        var anyCompany = Gates.Node(GateKinds.GrandCompanyRankAtLeast, amount: 5);
        Assert.Equal(GateOutcome.Satisfied, Outcome(anyCompany, Gates.Ctx(30, playerGrandCompany: 2, playerGrandCompanyRank: 5)));
        Assert.Equal(GateOutcome.Blocked, Outcome(anyCompany, Gates.Ctx(30, playerGrandCompany: 2, playerGrandCompanyRank: 4)));
        Assert.Equal(GateOutcome.Blocked, Outcome(anyCompany, Gates.Ctx(30)));

        var oneCompany = Gates.Node(GateKinds.GrandCompanyRankAtLeast, [1u], amount: 1, display: "the Maelstrom");
        Assert.Equal(GateOutcome.Blocked, Outcome(oneCompany, Gates.Ctx(30, playerGrandCompany: 2, playerGrandCompanyRank: 9)));
        Assert.Equal(GateOutcome.Satisfied, Outcome(oneCompany, Gates.Ctx(30, playerGrandCompany: 1, playerGrandCompanyRank: 1)));
        Assert.Equal(
            GateOutcome.Indeterminate,
            Outcome(Gates.Node(GateKinds.GrandCompanyRankAtLeast, [1u]), Gates.Ctx(30, playerGrandCompany: 1)));
    }

    /// <summary>Request-gated: the client holds no achievement data until it has asked the server,
    /// and an unasked table reads as "you have earned nothing at all".</summary>
    [Fact]
    public void AchievementComplete_UnfetchedTableIsIndeterminate_NotBlocked()
    {
        var node = Gates.Node(GateKinds.AchievementComplete, [2867u], display: "a title");
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(100)));
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(100, isAchievementComplete: _ => true)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(100, isAchievementComplete: _ => false)));
    }

    [Fact]
    public void AetherCurrentsComplete_Satisfied_Blocked_Indeterminate()
    {
        var node = Gates.Node(GateKinds.AetherCurrentsComplete, [12u], display: "Lakeland");
        Assert.Equal(GateOutcome.Satisfied, Outcome(node, Gates.Ctx(80, isAetherCurrentZoneComplete: id => id == 12)));
        Assert.Equal(GateOutcome.Blocked, Outcome(node, Gates.Ctx(80, isAetherCurrentZoneComplete: _ => false)));
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(80)));
    }

    /// <summary>Rank 0 is a legal Shared FATE rank, so an unarrived tab and a genuinely unranked
    /// zone would look identical to a two-valued reader.</summary>
    [Fact]
    public void SharedFateRankAtLeast_UnarrivedTabIsIndeterminate_NotRankZero()
    {
        var node = Gates.Node(GateKinds.SharedFateRankAtLeast, [813u], amount: 3, display: "Lakeland");
        Assert.Equal(GateOutcome.Indeterminate, Outcome(node, Gates.Ctx(80)));
        Assert.Equal(
            GateOutcome.Satisfied,
            Outcome(node, Gates.Ctx(80, sharedFateRankAtLeast: (_, rank) => rank <= 3)));
        Assert.Equal(
            GateOutcome.Blocked,
            Outcome(node, Gates.Ctx(80, sharedFateRankAtLeast: (_, _) => false)));
    }

    [Fact]
    public void ZoneProgressAtLeast_OutsideTheZoneIsIndeterminate()
    {
        var eureka = Gates.Node(
            GateKinds.ZoneProgressAtLeast, amount: 60, scope: GateKinds.ScopeEureka, display: "Eureka");
        Assert.Equal(GateOutcome.Indeterminate, Outcome(eureka, Gates.Ctx(70)));
        Assert.Equal(
            GateOutcome.Satisfied,
            Outcome(eureka, Gates.Ctx(70, zoneProgressAtLeast: (kind, _) => kind == ZoneProgressKind.EurekaElemental)));

        var bozja = Gates.Node(
            GateKinds.ZoneProgressAtLeast, amount: 10, scope: GateKinds.ScopeBozja, display: "Bozja");
        Assert.Equal(
            GateOutcome.Blocked,
            Outcome(bozja, Gates.Ctx(80, zoneProgressAtLeast: (_, _) => false)));
        Assert.Equal(
            GateOutcome.Indeterminate,
            Outcome(Gates.Node(GateKinds.ZoneProgressAtLeast, amount: 10, scope: "hydatos"), Gates.Ctx(80)));
    }

    [Fact]
    public void Unverifiable_IsAlwaysIndeterminate_AndCarriesItsSentence()
    {
        var node = Gates.Node(GateKinds.Unverifiable, display: "needs a festival that isn't running");
        var result = Registry.Evaluate(node, Gates.Ctx(100).Live);

        Assert.Equal(GateOutcome.Indeterminate, result.Outcome);
        Assert.Equal(UnlockStatus.RequirementsUnknown, result.Status);
        Assert.Equal("needs a festival that isn't running", result.Reason);
    }

    /// <summary>Every kind returns an EXISTING status when it blocks. The enum is the contract with
    /// the UI, and a gate that invented a value would be a gate the window cannot draw.</summary>
    [Fact]
    public void NoEvaluatorInventsAStatus()
    {
        var blocked = new[]
        {
            Outcome2(Gates.Node(GateKinds.MountOwned, [AnyId]), Gates.Ctx(1)),
            Outcome2(Gates.Node(GateKinds.MinionOwned, [AnyId]), Gates.Ctx(1)),
            Outcome2(Gates.Node(GateKinds.QuestComplete, [AnyId]), Gates.Ctx(1)),
            Outcome2(Gates.Node(GateKinds.CharacterLevelAtLeast, amount: 90), Gates.Ctx(1)),
        };

        Assert.All(blocked, r => Assert.True(Enum.IsDefined(r.Status)));
        Assert.All(blocked, r => Assert.NotEqual(UnlockStatus.Available, r.Status));
    }

    private static GateOutcome Outcome(GateNode node, UnlockGateContext ctx) =>
        Registry.Evaluate(node, ctx.Live).Outcome;

    private static GateResult Outcome2(GateNode node, UnlockGateContext ctx) =>
        Registry.Evaluate(node, ctx.Live);
}
