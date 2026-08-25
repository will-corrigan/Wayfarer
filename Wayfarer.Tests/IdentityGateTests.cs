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
