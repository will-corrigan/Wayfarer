using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The gates that live outside a Quest row's own columns, and the rule that replaced the
/// unconditional Available: this plugin says "I don't know" rather than sending a player to
/// something they cannot get.</summary>
public class UnlockGateTests
{
    private const uint FisherJobRowId = 18;

    // Quest #67086 "Fiery Wings, Fiery Hearts", exactly as the sheet ships it: level 1, every
    // gate column empty, and a Firebird for a reward. The seven mounts it really wants are
    // enforced by a server-side accept script that nothing in sqpack records.
    private static readonly UnlockRequirement.Collectible[] Lanners =
    [
        new(76, "Rose Lanner", "Thok ast Thok (Extreme)"),
        new(75, "White Lanner", "The Limitless Blue (Extreme)"),
        new(77, "Round Lanner", "The Singularity Reactor (Extreme)"),
        new(78, "Warring Lanner", "Containment Bay S1T7 (Extreme)"),
        new(90, "Dark Lanner", "The Minstrel's Ballad: Nidhogg's Rage"),
        new(98, "Sophic Lanner", "Containment Bay P1T6 (Extreme)"),
        new(104, "Demonic Lanner", "Containment Bay Z1T9 (Extreme)"),
    ];

    /// <summary>Curated blocks that carry no checkable requirement: an empty object, prose only,
    /// prose with an explicit unverifiable flag, and lists that exist but are empty.</summary>
    public static TheoryData<UnlockRequirement> RequirementsThatCheckNothing() =>
    [
        new UnlockRequirement(),
        new UnlockRequirement { Label = "something the wiki mentions but nobody wrote down" },
        new UnlockRequirement { Label = "a requirement nobody can check", Unverifiable = true },
        new UnlockRequirement { Mounts = [], Minions = [], Items = [], Jobs = [] },
    ];

    [Fact]
    public void FieryWingsFieryHearts_WithoutTheLanners_IsNotAvailable()
    {
        var u = Firebird();
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isMountUnlocked: _ => false));

        Assert.NotEqual(UnlockStatus.Available, u.Status);
        Assert.Equal(UnlockStatus.CollectionLocked, u.Status);
        Assert.Contains("Rose Lanner", u.LockReason, StringComparison.Ordinal);
        Assert.Contains("Thok ast Thok (Extreme)", u.LockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void FieryWingsFieryHearts_WithEveryLanner_IsAvailable()
    {
        var u = Firebird();
        var owned = Lanners.Select(l => l.Id).ToHashSet();
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isMountUnlocked: owned.Contains));

        Assert.Equal(UnlockStatus.Available, u.Status);
        Assert.Empty(u.MissingRequirements);
    }

    [Fact]
    public void CollectionGate_PartiallyOwned_CountsWhatIsLeftAndNamesTheNextOne()
    {
        var u = Firebird();
        var owned = new HashSet<uint> { 76, 75 };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isMountUnlocked: owned.Contains));

        Assert.Equal(UnlockStatus.CollectionLocked, u.Status);
        Assert.Equal(5, u.MissingRequirements.Count);
        Assert.Contains("5 more", u.LockReason, StringComparison.Ordinal);
        Assert.Contains("Round Lanner", u.LockReason, StringComparison.Ordinal);
        Assert.DoesNotContain("Rose Lanner", u.LockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionGate_ListsEveryMissingRequirement_NotJustTheFirst()
    {
        var u = Firebird();
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(7, u.MissingRequirements.Count);
        Assert.Equal("Rose Lanner — Thok ast Thok (Extreme)", u.MissingRequirements[0]);
        Assert.Equal("Demonic Lanner — Containment Bay Z1T9 (Extreme)", u.MissingRequirements[6]);
    }

    [Fact]
    public void CuratedRequirement_Unverifiable_IsRequirementsUnknown_NeverAvailable()
    {
        var u = Make("Emanation (Extreme) Trial Access", 67000, 70);
        u.Def.Requires = new UnlockRequirement
        {
            Label = "unlocked in game by talking to an NPC about Lakshmi",
            Unverifiable = true,
        };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.NotEqual(UnlockStatus.Available, u.Status);
        Assert.Equal(UnlockStatus.RequirementsUnknown, u.Status);
        Assert.Contains("talking to an NPC about Lakshmi", u.LockReason, StringComparison.Ordinal);
    }

    /// <summary>The red-proof fixture: a player who has done everything this plugin CAN check —
    /// the prerequisite quest, the level, no other gate in the way — must be told the ceremony is
    /// <see cref="UnlockStatus.Available"/>, with the partner condition named alongside it rather
    /// than used to withhold Available. A couple who both play the game are not told their own
    /// wedding is out of reach for a fact this plugin will never be able to confirm.</summary>
    [Fact]
    public void EternalBonding_EverythingCheckableMet_IsAvailableWithTheConditionNamed()
    {
        var u = EternalBonding();
        var all = new List<ResolvedUnlock> { u };

        // Every gate ahead of the curated requirement is satisfied: no lockout, level met, the
        // prerequisite quest ("The Scions of the Seventh Dawn") complete.
        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 66045));

        Assert.Equal(UnlockStatus.Available, u.Status);
        Assert.Equal("needs a partner", u.AvailableCondition);
        Assert.NotNull(u.AvailableConditionDetail);
        Assert.Contains("partner", UnlockStatusDisplay.Sentence(u), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Available", UnlockStatusDisplay.Sentence(u), StringComparison.Ordinal);
    }

    [Fact]
    public void EternalBonding_PrerequisiteQuestIncomplete_IsQuestLocked_BeforeThePartnerGateIsEvenReached()
    {
        var u = EternalBonding();
        u.PrereqRowIds = [66045];
        u.PrereqNames = ["The Scions of the Seventh Dawn"];
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.QuestLocked, u.Status);
        Assert.Contains("The Scions of the Seventh Dawn", u.LockReason, StringComparison.Ordinal);
        Assert.Null(u.AvailableCondition);
    }

    [Fact]
    public void CuratedRequirement_RequiresAnotherPlayer_IsAvailableWithCondition_NeverBlocked_AndDistinctFromUnverifiable()
    {
        var u = Make("Some Duo-Only Thing", 70002, 50);
        u.Def.Requires = new UnlockRequirement { Label = "a partner", RequiresAnotherPlayer = true };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.Available, u.Status);
        Assert.NotEqual(UnlockStatus.RequirementsUnknown, u.Status);
        Assert.Null(u.LockReason);
        Assert.Equal("needs a partner", u.AvailableCondition);
        Assert.Equal("a partner", u.AvailableConditionDetail);
    }

    [Fact]
    public void CuratedRequirement_RequiresAnotherPlayer_WithNoLabelOrConditionSource_HasAPlainFallbackDetail()
    {
        var u = Make("Some Duo-Only Thing", 70002, 50);
        u.Def.Requires = new UnlockRequirement { RequiresAnotherPlayer = true };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.Available, u.Status);
        Assert.Equal("needs a partner", u.AvailableCondition);
        Assert.Equal("The game does not say more than that.", u.AvailableConditionDetail);
    }

    /// <summary>When a <see cref="GameTextRef"/> is curated, the runtime lookup is preferred over
    /// the curated label — the whole point of quoting the game rather than paraphrasing it.</summary>
    [Fact]
    public void CuratedRequirement_RequiresAnotherPlayer_WithConditionSource_PrefersTheResolvedGameText()
    {
        var u = Make("Some Duo-Only Thing", 70002, 50);
        var source = new GameTextRef("HowToPage", 1861, 4);
        u.Def.Requires = new UnlockRequirement { Label = "a partner", RequiresAnotherPlayer = true, ConditionSource = source };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(playerLevel: 90, resolveGameText: r => r == source ? "You are currently in a party with your partner." : null));

        Assert.Equal(UnlockStatus.Available, u.Status);
        Assert.Equal("You are currently in a party with your partner.", u.AvailableConditionDetail);
    }

    /// <summary>A missed runtime lookup (a bad reference, a sheet Lumina can't resolve, the
    /// resolver itself being null in a caller that never wires one) falls back to the curated
    /// label rather than surfacing nothing.</summary>
    [Fact]
    public void CuratedRequirement_RequiresAnotherPlayer_WithConditionSourceThatMisses_FallsBackToLabel()
    {
        var u = Make("Some Duo-Only Thing", 70002, 50);
        u.Def.Requires = new UnlockRequirement
        {
            Label = "a partner",
            RequiresAnotherPlayer = true,
            ConditionSource = new GameTextRef("HowToPage", 1861, 4),
        };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, resolveGameText: _ => null));

        Assert.Equal(UnlockStatus.Available, u.Status);
        Assert.Equal("a partner", u.AvailableConditionDetail);
    }

    /// <summary>A partner requirement takes precedence over the generic Unverifiable fallback
    /// when (mistakenly, or by a future data change) both are set — see the same rule enforced
    /// at build time by data/validate-unlocks.mjs, which rejects the combination outright. The
    /// calculator's own precedence is a second line of defence.</summary>
    [Fact]
    public void CuratedRequirement_RequiresAnotherPlayer_TakesPrecedenceOverUnverifiable()
    {
        var u = Make("Some Duo-Only Thing", 70002, 50);
        u.Def.Requires = new UnlockRequirement { Label = "a partner", RequiresAnotherPlayer = true, Unverifiable = true };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.Available, u.Status);
    }

    [Fact]
    public void NoDiscoverableGate_WithNothingCurated_IsRequirementsUnknown_NeverAvailable()
    {
        // The regression guard: a future expansion's trophy mount will look exactly like this
        // until someone curates it, and it must not be advertised as ready to pick up.
        var u = Make("Some Future Trophy (Mount)", 71000, 1);
        u.HasNoDiscoverableGate = true;
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 100));

        Assert.NotEqual(UnlockStatus.Available, u.Status);
        Assert.Equal(UnlockStatus.RequirementsUnknown, u.Status);
    }

    /// <summary>The guard is disabled by a curated block that <i>checks</i> something, not by one
    /// that merely exists. A `requires` carrying only prose, or one whose collectible lists are all
    /// empty, is a note; treating its presence as evidence would reopen the exact hole the guard
    /// above closes, with no validator error to warn anyone.</summary>
    [Theory]
    [MemberData(nameof(RequirementsThatCheckNothing))]
    public void NoDiscoverableGate_WithACuratedBlockThatChecksNothing_IsStillRequirementsUnknown(
        UnlockRequirement requires)
    {
        var u = Make("Some Future Trophy (Mount)", 71000, 1);
        u.HasNoDiscoverableGate = true;
        u.Def.Requires = requires;
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 100));

        Assert.NotEqual(UnlockStatus.Available, u.Status);
        Assert.Equal(UnlockStatus.RequirementsUnknown, u.Status);
    }

    [Fact]
    public void NoDiscoverableGate_WithCuratedRequirementsMet_IsAvailable()
    {
        var u = Firebird();
        u.HasNoDiscoverableGate = true;
        var owned = Lanners.Select(l => l.Id).ToHashSet();
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isMountUnlocked: owned.Contains));

        Assert.Equal(UnlockStatus.Available, u.Status);
    }

    [Fact]
    public void HardRequiredJob_TooLow_IsLevelLocked()
    {
        // Spearfishing: ClassJobRequired is Fisher, and the quest's own category mask lets every
        // job through, so this showed as available to a character who had never touched Fisher.
        var u = Make("Spearfishing Access", 68458, 61);
        u.HardRequiredJobRowId = FisherJobRowId;
        u.HardRequiredJobName = "Fisher";
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, getClassJobLevel: _ => 1));

        Assert.Equal(UnlockStatus.LevelLocked, u.Status);
        Assert.Equal("needs Fisher 61", u.LockReason);
    }

    [Fact]
    public void HardRequiredJob_AtLevel_IsAvailable()
    {
        var u = Make("Spearfishing Access", 68458, 61);
        u.HardRequiredJobRowId = FisherJobRowId;
        u.HardRequiredJobName = "Fisher";
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, getClassJobLevel: id => id == FisherJobRowId ? 70 : 1));

        Assert.Equal(UnlockStatus.Available, u.Status);
    }

    [Fact]
    public void CuratedJobRequirement_TooLow_IsLevelLocked()
    {
        var u = Make("Spearfishing Access", 68458, 61);
        u.Def.Requires = new UnlockRequirement
        {
            Label = "Fisher level 61",
            Jobs = [new UnlockRequirement.RequiredJob(FisherJobRowId, "Fisher", 61)],
        };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, getClassJobLevel: _ => 60));

        Assert.Equal(UnlockStatus.LevelLocked, u.Status);
        Assert.Equal("needs Fisher 61", u.LockReason);
    }

    [Fact]
    public void CuratedMinLevel_BelowIt_IsLevelLocked()
    {
        // The Bozjan front: the sheet says 71 in ClassJobLevel[0] and hides the other 9 in
        // QuestLevelOffset. The curated minimum is the second source that catches it either way.
        var u = Make("The Bozjan Southern Front Access", 69477, 80);
        u.Def.Requires = new UnlockRequirement { Label = "level 80", MinLevel = 80 };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 75));

        Assert.Equal(UnlockStatus.LevelLocked, u.Status);
        Assert.Equal("needs level 80", u.LockReason);
    }

    [Fact]
    public void AcceptCondition_IncompleteRequirement_IsQuestLocked_NamingIt()
    {
        var u = Make("Golden Dhyata minion", 70010, 90);
        u.AcceptConditionQuestRowIds = [69695, 69702];
        u.AcceptConditionQuestNames = ["The Culture of Love", "Pastures New"];
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 69695));

        Assert.Equal(UnlockStatus.QuestLocked, u.Status);
        Assert.Equal("needs quest 'Pastures New'", u.LockReason);
    }

    [Fact]
    public void AcceptCondition_AllComplete_IsAvailable()
    {
        var u = Make("Golden Dhyata minion", 70010, 90);
        u.AcceptConditionQuestRowIds = [69695, 69702];
        u.AcceptConditionQuestNames = ["The Culture of Love", "Pastures New"];
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isQuestComplete: id => id is 69695 or 69702));

        Assert.Equal(UnlockStatus.Available, u.Status);
    }

    [Fact]
    public void AcceptCondition_UnresolvedRequirementId_IsRequirementsUnknown()
    {
        // Some requirement ids in that sheet are small numbers that resolve to no Quest row.
        // That is a requirement we can't identify, not one that isn't there.
        var u = Make("Some Delivery Access", 70115, 90);
        u.HasUnresolvedAcceptCondition = true;
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.NotEqual(UnlockStatus.Available, u.Status);
        Assert.Equal(UnlockStatus.RequirementsUnknown, u.Status);
    }

    [Fact]
    public void AmbiguousQuestName_NotComplete_IsRequirementsUnknown_NotAvailable()
    {
        // Three live "Simply the Hest" rows, one per starting city. Reporting either "available"
        // or "not done" would be a guess about which city this character started in.
        var u = Make("Guildhests", 65594, 10);
        u.AlternativeQuestRowIds = [65594, 65595, 65596];
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.RequirementsUnknown, u.Status);
        Assert.Contains("3 quests share this name", u.LockReason, StringComparison.Ordinal);
    }

    /// <summary>The ordering, not just the outcome. Every gate below Accepted reads one Quest row,
    /// and when the matcher had to pick that row arbitrarily its prerequisites belong to a quest
    /// this character may never have been offered. Reporting one by name is a confident, specific,
    /// wrong statement — worse than admitting the ambiguity.</summary>
    [Fact]
    public void AmbiguousQuestName_WithAPrereqOnTheBoundRow_ReportsTheAmbiguity_NotThePrereq()
    {
        var u = Make("Guildhests", 65594, 10);
        u.AlternativeQuestRowIds = [65594, 65595, 65596];
        u.PrereqRowIds = [66000];
        u.PrereqNames = ["Coming to Limsa Lominsa"];
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.RequirementsUnknown, u.Status);
        Assert.DoesNotContain("Coming to Limsa Lominsa", u.LockReason, StringComparison.Ordinal);
    }

    /// <summary>Same, for the level gate — the other row-specific verdict an arbitrary binding can
    /// get wrong.</summary>
    [Fact]
    public void AmbiguousQuestName_BelowTheBoundRowsLevel_ReportsTheAmbiguity_NotTheLevel()
    {
        var u = Make("Guildhests", 65594, 60);
        u.AlternativeQuestRowIds = [65594, 65595, 65596];
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20));

        Assert.Equal(UnlockStatus.RequirementsUnknown, u.Status);
    }

    [Fact]
    public void AmbiguousQuestName_SiblingAccepted_IsAccepted()
    {
        var u = Make("Guildhests", 65594, 10);
        u.AlternativeQuestRowIds = [65594, 65595, 65596];
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isQuestAccepted: id => id == 65596));

        Assert.Equal(UnlockStatus.Accepted, u.Status);
    }

    [Fact]
    public void LevelGateStillWinsOverTheCollectionGate()
    {
        var u = Firebird();
        u.QuestLevel = 50;
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20));

        Assert.Equal(UnlockStatus.LevelLocked, u.Status);
    }

    [Fact]
    public void CuratedItemRequirement_ChecksTheRightContainer()
    {
        var u = Make("Some Key Item Gate", 70000, 50);
        u.Def.Requires = new UnlockRequirement
        {
            Label = "the key item this quest wants",
            Items = [new UnlockRequirement.RequiredItem(2002052, "Firebird Whistle", 1, true)],
        };
        var all = new List<ResolvedUnlock> { u };

        // Present in the bags but not in key items: still missing, because that is where it lives.
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, getOwnedItemCount: _ => 1));
        Assert.Equal(UnlockStatus.CollectionLocked, u.Status);

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, getKeyItemCount: _ => 1));
        Assert.Equal(UnlockStatus.Available, u.Status);
    }

    [Fact]
    public void CuratedMinionRequirement_IsChecked()
    {
        var u = Make("Some Minion Gate", 70001, 50);
        u.Def.Requires = new UnlockRequirement
        {
            Label = "a minion",
            Minions = [new UnlockRequirement.Collectible(42, "Wind-up Kirin", "a raid")],
        };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));
        Assert.Equal(UnlockStatus.CollectionLocked, u.Status);
        Assert.Equal("requires Wind-up Kirin — a raid", u.LockReason);

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isMinionUnlocked: id => id == 42));
        Assert.Equal(UnlockStatus.Available, u.Status);
    }

    // Quest #67114 "The Ties That Bind", exactly as the sheet ships it: PreviousQuest points at
    // "The Scions of the Seventh Dawn" (66045), which the ordinary prereq gate already checks —
    // and still needs a second, physically present player to actually hold the ceremony, which
    // no sheet column or client API records. See docs/superpowers/specs/2026-08-23-*.
    private static ResolvedUnlock EternalBonding()
    {
        var u = Make("Ceremony of Eternal Bonding", 67114, 50);
        u.Def.Requires = new UnlockRequirement
        {
            Label = "same Home World, party of two, both wearing a Promise Wristlet, in East Shroud",
            RequiresAnotherPlayer = true,
        };
        return u;
    }

    private static ResolvedUnlock Firebird()
    {
        var u = Make("Firebird (Mount)", 67086, 1);
        u.Def.Requires = new UnlockRequirement
        {
            Label = "all seven Heavensward Extreme-trial Lanner mounts",
            Mounts = [.. Lanners],
        };
        return u;
    }

    private static ResolvedUnlock Make(string unlock, uint rowId, int questLevel) => new()
    {
        Def = new UnlockDefinition { Unlock = unlock, Type = "mount", Quest = "q" },
        QuestRowId = rowId,
        QuestLevel = questLevel,
    };
}
