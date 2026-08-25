using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;

namespace Wayfarer.Tests;

/// <summary>The gate that reads an entry's OWN identity, and the guards around what it is allowed
/// to overrule.
///
/// <para>Thirty-six catalogue entries said, in the data, that whether the player had taken their
/// unlock was unreadable. It was readable all along — the client keeps a bit per duty — and these
/// fixtures pin both halves of the correction: that a readable identity grades the entry, and that
/// an unreadable one changes nothing at all.</para></summary>
public class IdentityGateTests
{
    private const uint TargetDutyId = 20051;
    private const uint PrerequisiteDutyId = 20050;

    [Fact]
    public void IdentityDutyUnlocked_IsDone()
    {
        var all = new List<ResolvedUnlock> { TrialAccess() };
        UnlockStatusCalculator.Compute(
            all, Gates.Ctx(100, isInstanceContentUnlocked: id => id == TargetDutyId));

        Assert.Equal(UnlockStatus.Done, all[0].Status);
    }

    /// <summary>The other half of the win, and the larger one: not unlocked, prerequisite cleared,
    /// so the player can go and take it. This entry used to report "requirements unknown" here.</summary>
    [Fact]
    public void IdentityDutyLocked_PrerequisiteCleared_IsAvailable()
    {
        var all = new List<ResolvedUnlock> { TrialAccess() };
        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(
                100,
                isInstanceContentUnlocked: _ => false,
                isInstanceContentCompleted: id => id == PrerequisiteDutyId));

        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void IdentityDutyLocked_PrerequisiteNotCleared_NamesThePrerequisite()
    {
        var all = new List<ResolvedUnlock> { TrialAccess() };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(100, isInstanceContentUnlocked: _ => false));

        Assert.Equal(UnlockStatus.InstanceLocked, all[0].Status);
        Assert.Equal("requires clearing the Jade Stoa", all[0].LockReason);
    }

    /// <summary>The regression guard. An entry whose every checkable gate passes but which still
    /// carries an unverifiable requirement does NOT read as plainly Available — the curated shrug
    /// only gives way where an identity gate actually answered the question it was hedging about,
    /// and this entry has no identity gate at all.</summary>
    [Fact]
    public void NoIdentityGate_EverythingCheckableMet_IsStillNotPlainlyAvailable()
    {
        var all = new List<ResolvedUnlock> { TrialAccess(withIdentity: false) };
        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(
                100,
                isInstanceContentUnlocked: _ => true,
                isInstanceContentCompleted: id => id == PrerequisiteDutyId));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);
        Assert.NotEqual(UnlockStatus.Available, all[0].Status);
    }

    /// <summary>An identity gate that cannot be READ is not an identity gate that said no. Mid
    /// load, or with a scope this build has no reader for, the entry falls back to exactly what it
    /// said before this feature existed.</summary>
    [Fact]
    public void IdentityGateIndeterminate_LeavesTheEntryUnknown()
    {
        var entry = TrialAccess();
        entry.IdentityGate!.Scope = GateKinds.ScopePublic;
        var all = new List<ResolvedUnlock> { entry };

        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(
                100,
                isInstanceContentUnlocked: _ => true,
                isInstanceContentCompleted: id => id == PrerequisiteDutyId));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);
    }

    /// <summary>The whole pass is skipped when live state is not ready, so a title screen cannot
    /// rewrite a correct checklist into the claim that the player owns nothing.</summary>
    [Fact]
    public void NotReady_LeavesStatusesUntouched()
    {
        var all = new List<ResolvedUnlock> { TrialAccess() };
        all[0].Status = UnlockStatus.Done;
        all[0].LockReason = "kept";

        UnlockStatusCalculator.Compute(all, Gates.Ctx(100, liveStateReady: false));

        Assert.Equal(UnlockStatus.Done, all[0].Status);
        Assert.Equal("kept", all[0].LockReason);
    }

    /// <summary>An entry with a quest of its own keeps its existing precedence: the level gate is
    /// still read before anything the catalogue curated, and an identity gate that says "not yet"
    /// does not jump the queue.</summary>
    [Fact]
    public void Precedence_LevelGateStillWinsOverTheGateTree()
    {
        var all = new List<ResolvedUnlock>
        {
            new()
            {
                Def = new UnlockDefinition { Unlock = "A Levelled Thing", Type = "trial", Level = 70 },
                QuestRowId = 70000,
                QuestLevel = 70,
                IdentityGate = Gates.Node(
                    GateKinds.DutyUnlocked, [TargetDutyId], scope: GateKinds.ScopeInstance, display: "a trial"),
            },
        };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(50, isInstanceContentUnlocked: _ => false));

        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
    }

    /// <summary>The other half of that guard, and the one the shipped catalogue actually hits. An
    /// identity gate that says "you have not taken this unlock" retires the curated shrug because
    /// everything checkable has already passed — but on an entry with nothing checkable at all there
    /// is nothing to have passed, and "you have not got it" says nothing about whether you can go and
    /// get it. Such an entry read plainly Available, with the gold "go and do this" marker, pointing
    /// at no quest, no giver, no level gate and no route — derived from a field whose whole content is
    /// that the requirement is unknown.
    ///
    /// <para><c>The Final Verse Access</c> is exactly this shape in the catalogue today: a label and
    /// <c>unverifiable</c>, a quest name that does not bind, and a duty reward that gives it an
    /// identity gate.</para></summary>
    [Fact]
    public void IdentityDutyLocked_NothingElseCheckable_IsStillNotPlainlyAvailable()
    {
        var all = new List<ResolvedUnlock> { UnreadableAccess() };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(100, isInstanceContentUnlocked: _ => false));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);
        Assert.Equal("the catalogue does not record what this needs", all[0].LockReason);
    }

    /// <summary>And the identity gate still grades the same entry when it CAN answer the half it is
    /// authoritative about: owning the unlock is owning it, whatever the catalogue failed to say
    /// about how to get there.</summary>
    [Fact]
    public void IdentityDutyUnlocked_NothingElseCheckable_IsStillDone()
    {
        var all = new List<ResolvedUnlock> { UnreadableAccess() };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(100, isInstanceContentUnlocked: id => id == TargetDutyId));

        Assert.Equal(UnlockStatus.Done, all[0].Status);
    }

    /// <summary>An entry that carries prose and nothing else: no duties, no items, no gates, no level
    /// requirement, and a quest name the game's data does not know — so no quest row binds and
    /// nothing about it is checkable. Its duty reward still earns it an identity gate.</summary>
    private static ResolvedUnlock UnreadableAccess() => new()
    {
        Def = new UnlockDefinition
        {
            Unlock = "An Unreadable Access",
            Type = "dungeon",
            Level = 91,
            Reward = new UnlockReward("ContentFinderCondition", 1065, "an unreadable duty"),
            Requires = new UnlockRequirement
            {
                Label = "the catalogue does not record what this needs",
                Unverifiable = true,
            },
        },
        IdentityGate = Gates.Node(
            GateKinds.DutyUnlocked,
            [TargetDutyId],
            scope: GateKinds.ScopeInstance,
            display: "an unreadable duty"),
    };

    private static ResolvedUnlock TrialAccess(bool withIdentity = true) => new()
    {
        Def = new UnlockDefinition
        {
            Unlock = "The Jade Stoa (Extreme) Trial Access",
            Type = "trial",
            Level = 70,
            Reward = new UnlockReward("ContentFinderCondition", 291, "the Jade Stoa (Extreme)"),
            Requires = new UnlockRequirement
            {
                Label = "unlocked by clearing the Jade Stoa",
                Duties = [new UnlockRequirement.Collectible(PrerequisiteDutyId, "the Jade Stoa", null)],
                Unverifiable = true,
            },
        },
        IdentityGate = withIdentity
            ? Gates.Node(
                GateKinds.DutyUnlocked,
                [TargetDutyId],
                scope: GateKinds.ScopeInstance,
                display: "the Jade Stoa (Extreme)")
            : null,
    };
}
