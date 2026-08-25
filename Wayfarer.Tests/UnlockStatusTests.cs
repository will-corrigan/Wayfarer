using System.Text.Json;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

public class UnlockStatusTests
{
    private const uint WeaverJobRowId = 44;
    private const uint CulinarianJobRowId = 45;
    private const uint BotanistJobRowId = 46;
    private const uint WarriorJobRowId = 47;
    private const uint MinerJobRowId = 48;
    private const uint DungeonInstanceContentId = 3000;
    private static readonly uint[] DisciplesOfTheHandJobRowIds = [50, 51, 52, 53, 54, 55, 56, 57];

    /// <summary>The six duplicated labels in the shipped catalogue that are progression tiers
    /// rather than alternative quests, with their real levels. Each tier is a different quest;
    /// completing one says nothing about the others.</summary>
    public static TheoryData<string, int[]> RealTierGroups() => new()
    {
        { "Sightseeing Log Expansion", [52, 60, 70, 80, 90] },
        { "Stone, Sky, Sea Access", [60, 70, 80, 90, 100] },
        { "Main Scenario Quest Continuation", [60, 70, 80, 90] },
        { "Role Quests Access", [70, 85, 92] },
        { "Levequest Expansion", [70, 80, 90] },
        { "Relic Gear Access", [89, 99] },
    };

    [Fact]
    public void UnmatchedIsUnverified()
    {
        var all = new List<ResolvedUnlock> { Make("Mystery", null, 10) };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 50));
        Assert.Equal(UnlockStatus.Unverified, all[0].Status);
    }

    [Fact]
    public void CompleteIsDone_EvenAboveLevel()
    {
        var all = new List<ResolvedUnlock> { Make("Glamours", 65754, 90) };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 15, isQuestComplete: id => id == 65754));
        Assert.Equal(UnlockStatus.Done, all[0].Status);
    }

    [Fact]
    public void AlternativeComplete_MarksAllDone()
    {
        var a = Make("Glamours", 100, 15);
        var b = Make("Glamours", 200, 15);
        var all = new List<ResolvedUnlock> { a, b };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20, isQuestComplete: id => id == 200));
        Assert.Equal(UnlockStatus.Done, a.Status);
        Assert.Equal(UnlockStatus.Done, b.Status);
    }

    [Theory]
    [MemberData(nameof(RealTierGroups))]
    public void CompletingTheLowestTier_LeavesEveryHigherTierNotDone(string label, int[] levels)
    {
        var tiers = levels.Select((lv, i) => Tier(label, lv, (uint)(9000 + i))).ToList();
        UnlockStatusCalculator.Compute(
            tiers, Gates.Ctx(playerLevel: 110, isQuestComplete: id => id == 9000));

        Assert.Equal(UnlockStatus.Done, tiers[0].Status);
        for (var i = 1; i < tiers.Count; i++)
        {
            Assert.NotEqual(UnlockStatus.Done, tiers[i].Status);
        }
    }

    [Theory]
    [MemberData(nameof(RealTierGroups))]
    public void CompletingTheHighestTier_LeavesEveryLowerTierNotDone(string label, int[] levels)
    {
        var top = (uint)(9000 + levels.Length - 1);
        var tiers = levels.Select((lv, i) => Tier(label, lv, (uint)(9000 + i))).ToList();
        UnlockStatusCalculator.Compute(
            tiers, Gates.Ctx(playerLevel: 110, isQuestComplete: id => id == top));

        Assert.Equal(UnlockStatus.Done, tiers[^1].Status);
        for (var i = 0; i < tiers.Count - 1; i++)
        {
            Assert.NotEqual(UnlockStatus.Done, tiers[i].Status);
        }
    }

    /// <summary>The other half of the same rule: entries sharing a label <i>and</i> a level really
    /// are one unlock reached by different quests, and completing any one of them completes it.
    /// These are the shipped catalogue's own two genuine cases.</summary>
    [Theory]
    [InlineData("Levequests", 10, 3)]
    [InlineData("Glamours", 15, 2)]
    public void AlternativeQuestsAtTheSameLevel_AllReportDone(string label, int level, int count)
    {
        var group = Enumerable.Range(0, count).Select(i => Tier(label, level, (uint)(9100 + i))).ToList();
        UnlockStatusCalculator.Compute(
            group, Gates.Ctx(playerLevel: 30, isQuestComplete: id => id == 9101));

        Assert.All(group, u => Assert.Equal(UnlockStatus.Done, u.Status));
    }

    /// <summary>Completion evidence belongs to a quest. An entry the matcher could not bind to any
    /// quest row has none of its own and may not borrow a sibling's.</summary>
    [Fact]
    public void EntryWithNoQuest_IsNeverDone_EvenWhenASiblingIs()
    {
        var bound = Tier("Sightseeing Log Expansion", 52, 9000);
        var unbound = Tier("Sightseeing Log Expansion", 52, null);
        var all = new List<ResolvedUnlock> { bound, unbound };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 110, isQuestComplete: id => id == 9000));

        Assert.Equal(UnlockStatus.Done, bound.Status);
        Assert.Equal(UnlockStatus.Unverified, unbound.Status);
    }

    [Fact]
    public void AcceptedBeatsAvailable()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10) };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20, isQuestAccepted: id => id == 100));
        Assert.Equal(UnlockStatus.Accepted, all[0].Status);
    }

    [Fact]
    public void AboveLevel_IsLevelLocked_WithoutPrereqChecks()
    {
        var prereqChecked = false;
        var all = new List<ResolvedUnlock> { Make("X", 100, 60, 900) };
        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(
                playerLevel: 20,
                isQuestComplete: id =>
                {
                    if (id == 900)
                    {
                        prereqChecked = true;
                    }

                    return false;
                }));
        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
        Assert.Equal("needs level 60", all[0].LockReason);
        Assert.False(prereqChecked);
    }

    [Fact]
    public void IncompletePrereq_IsQuestLocked_WithName()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10, 900, 901) };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20, isQuestComplete: id => id == 900));
        Assert.Equal(UnlockStatus.QuestLocked, all[0].Status);
        Assert.Equal("needs quest 'Quest 901'", all[0].LockReason);
    }

    [Fact]
    public void PrereqJoinOr_AnyOneComplete_Unblocks()
    {
        var u = Make("X", 100, 10, 900, 901);
        u.PrereqJoin = 2;
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20, isQuestComplete: id => id == 901));
        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void PrereqJoinOr_NoneComplete_QuestLocked_NamesBoth()
    {
        var u = Make("X", 100, 10, 900, 901);
        u.PrereqJoin = 2;
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20));
        Assert.Equal(UnlockStatus.QuestLocked, all[0].Status);
        Assert.Equal("needs quest 'Quest 900' or 'Quest 901'", all[0].LockReason);
    }

    [Fact]
    public void EverythingMet_IsAvailable()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10, 900) };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20, isQuestComplete: id => id == 900));
        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void QuestLock_AnyComplete_IsLockedOut()
    {
        var u = Make("X", 100, 10);
        u.LockoutQuestRowIds = [700, 701];
        u.LockoutQuestNames = ["Path A", "Path B"];
        u.LockoutJoin = 2;
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20, isQuestComplete: id => id == 701));
        Assert.Equal(UnlockStatus.LockedOut, all[0].Status);
        Assert.Equal("no longer obtainable — 'Path B' already completed", all[0].LockReason);
    }

    [Fact]
    public void QuestLock_TakesPrecedenceOverLevelGate()
    {
        var u = Make("X", 100, 90);
        u.LockoutQuestRowIds = [700];
        u.LockoutQuestNames = ["Path A"];
        u.LockoutJoin = 2;
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 1, isQuestComplete: id => id == 700));
        Assert.Equal(UnlockStatus.LockedOut, all[0].Status);
    }

    [Fact]
    public void QuestLock_NoneComplete_DoesNotLock()
    {
        var u = Make("X", 100, 10);
        u.LockoutQuestRowIds = [700];
        u.LockoutQuestNames = ["Path A"];
        u.LockoutJoin = 2;
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 20));
        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void ClassJobCategory_RowZero_IsUnrestricted_UsesActiveJobLevel()
    {
        var u = Make("X", 100, 50);
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 55));
        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void LiveBug_DoH50Quest_PlayerCombat90CrafterLevel1_IsLevelLocked_NotAvailable()
    {
        // Reproduces the reported bug: a level-50 Weaver quest was showing Available because the
        // old calculator checked the player's overall/active-job level (90, from combat) instead
        // of the specific job the quest actually requires (Weaver, level 1).
        var u = Make("Diadochos", 12345, 50);
        u.RequiredJobRowIds = [WeaverJobRowId];
        u.RequiredJobNames = ["Weaver"];
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(
            playerLevel: 90,
            getClassJobLevel: jobId => jobId == WeaverJobRowId ? 1 : 0);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
        Assert.Equal("needs Weaver Lv. 50", all[0].LockReason);
    }

    [Fact]
    public void MultiJobCategory_UsesMaxLevelAmongFlaggedJobs()
    {
        var u = Make("X", 100, 50);
        u.RequiredJobRowIds = [WeaverJobRowId, CulinarianJobRowId];
        u.RequiredJobNames = ["Weaver", "Culinarian"];
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(
            playerLevel: 90,
            getClassJobLevel: jobId => jobId switch
            {
                WeaverJobRowId => 10,
                CulinarianJobRowId => 60,
                _ => 0,
            });
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void MultiJobCategory_NeitherJobMeetsLevel_NamesBothJobs()
    {
        var u = Make("X", 100, 50);
        u.RequiredJobRowIds = [WeaverJobRowId, CulinarianJobRowId];
        u.RequiredJobNames = ["Weaver", "Culinarian"];
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, getClassJobLevel: _ => 5);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
        Assert.Equal("needs Weaver or Culinarian Lv. 50", all[0].LockReason);
    }

    [Fact]
    public void LiveBug_SentinelCategory1_IgnoredWhenLevel1IsZero_IsLevelLocked_NotAvailable()
    {
        // Live-data-verified shape ("Seeds of Hope", a BTN job quest, confirmed by a full scan of
        // the live Quest sheet): ClassJobCategory0 = {BTN} at ClassJobLevel[0] = 50,
        // ClassJobCategory1 = the "every job" sentinel mask (every job flagged true, including
        // Warrior) at ClassJobLevel[1] = 0. An earlier version of this fix unioned category0 and
        // category1's allowed-job sets, which folded that sentinel into the primary gate and
        // wrongly reopened this quest to every job (reproduced and proven red against the shipped
        // calculator before this fixture was corrected). ClassJobLevel[1] == 0 means category1
        // must be ignored entirely regardless of its content, so a Warrior-90/Botanist-1 player
        // must still be LevelLocked against Botanist alone.
        var u = Make("Seeds of Hope", 12345, 50);
        u.RequiredJobRowIds = [BotanistJobRowId];
        u.RequiredJobNames = ["Botanist"];
        u.AltRequiredJobRowIds = [.. DisciplesOfTheHandJobRowIds, BotanistJobRowId, WarriorJobRowId]; // the sentinel: every job, including Warrior
        u.AltRequiredJobNames = ["every job"];
        u.AltRequiredJobLevel = 0; // the "category1 isn't real" flag
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(
            playerLevel: 90,
            getClassJobLevel: jobId => jobId switch
            {
                WarriorJobRowId => 90,
                BotanistJobRowId => 1,
                _ => 0,
            });
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
        Assert.Equal("needs Botanist Lv. 50", all[0].LockReason);
    }

    [Fact]
    public void RealCategory1Alternative_EitherCategoryMetSuffices()
    {
        // Live-data-verified shape ("Reach for the Starboard"-style crafter/gatherer chain):
        // ClassJobCategory0 = {8 Disciples of the Hand} at ClassJobLevel[0] = 1, ClassJobCategory1
        // = {Miner} at ClassJobLevel[1] = 1 — a genuine independent alternative, not a sentinel.
        var u = Make("Reach for the Starboard", 12345, 1);
        u.RequiredJobRowIds = [.. DisciplesOfTheHandJobRowIds];
        u.RequiredJobNames = ["Carpenter", "Blacksmith", "Armorer", "Goldsmith", "Leatherworker", "Weaver", "Alchemist", "Culinarian"];
        u.AltRequiredJobRowIds = [MinerJobRowId];
        u.AltRequiredJobNames = ["Miner"];
        u.AltRequiredJobLevel = 1;

        // A player with only Miner at level 1 (no Disciple of the Hand levels at all) qualifies
        // via the real category1 alternative.
        var minerOnly = new List<ResolvedUnlock> { u };
        var minerCtx = Gates.Ctx(playerLevel: 90, getClassJobLevel: jobId => jobId == MinerJobRowId ? 1 : 0);
        UnlockStatusCalculator.Compute(minerOnly, minerCtx);
        Assert.Equal(UnlockStatus.Available, minerOnly[0].Status);

        // A player with only Warrior at level 90 (neither category) is locked out of both.
        var warriorOnly = new List<ResolvedUnlock> { u };
        var warriorCtx = Gates.Ctx(playerLevel: 90, getClassJobLevel: jobId => jobId == WarriorJobRowId ? 90 : 0);
        UnlockStatusCalculator.Compute(warriorOnly, warriorCtx);
        Assert.Equal(UnlockStatus.LevelLocked, warriorOnly[0].Status);
    }

    [Fact]
    public void LiveBug_DungeonGatedMountQuest_NotUnlocked_IsInstanceLocked_NotAvailable()
    {
        // Reproduces the reported bug: a mount quest gated behind clearing a dungeon showed
        // Available because the old calculator never looked at Quest.InstanceContent at all.
        var u = Make("Ceremonial Mount", 54321, 1);
        u.InstanceContentRowIds = [DungeonInstanceContentId];
        u.InstanceContentNames = ["The Praetorium"];
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(
            playerLevel: 90,
            isInstanceContentUnlocked: _ => true,
            isInstanceContentCompleted: _ => false);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.InstanceLocked, all[0].Status);
        Assert.Equal("requires completing The Praetorium", all[0].LockReason);
    }

    [Fact]
    public void InstanceContent_NotEvenUnlocked_ReasonSaysUnlocking()
    {
        var u = Make("X", 100, 1);
        u.InstanceContentRowIds = [DungeonInstanceContentId];
        u.InstanceContentNames = ["The Praetorium"];
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, isInstanceContentUnlocked: _ => false, isInstanceContentCompleted: _ => false);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.InstanceLocked, all[0].Status);
        Assert.Equal("requires unlocking The Praetorium", all[0].LockReason);
    }

    [Fact]
    public void InstanceContent_JoinAnd_AllMustBeCleared()
    {
        var u = Make("X", 100, 1);
        u.InstanceContentRowIds = [1, 2];
        u.InstanceContentNames = ["Dungeon One", "Dungeon Two"];
        u.InstanceContentJoin = 1;
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, isInstanceContentCompleted: id => id == 1, isInstanceContentUnlocked: _ => true);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.InstanceLocked, all[0].Status);
        Assert.Equal("requires completing Dungeon Two", all[0].LockReason);
    }

    [Fact]
    public void InstanceContent_JoinOr_AnyOneClearedSuffices()
    {
        var u = Make("X", 100, 1);
        u.InstanceContentRowIds = [1, 2];
        u.InstanceContentNames = ["Dungeon One", "Dungeon Two"];
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, isInstanceContentCompleted: id => id == 1, isInstanceContentUnlocked: _ => true);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void GrandCompany_WrongCompany_IsGrandCompanyLocked()
    {
        var u = Make("X", 100, 1);
        u.RequiredGrandCompanyId = 2;
        u.RequiredGrandCompanyName = "Order of the Twin Adder";
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, playerGrandCompany: 1);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.GrandCompanyLocked, all[0].Status);
        Assert.Equal("needs Order of the Twin Adder membership", all[0].LockReason);
    }

    [Fact]
    public void GrandCompany_RightCompany_WrongRank_IsGrandCompanyLocked()
    {
        var u = Make("X", 100, 1);
        u.RequiredGrandCompanyId = 1;
        u.RequiredGrandCompanyRank = 5;
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, playerGrandCompany: 1, playerGrandCompanyRank: 2);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.GrandCompanyLocked, all[0].Status);
        Assert.Equal("needs Grand Company rank 5", all[0].LockReason);
    }

    [Fact]
    public void GrandCompany_Met_IsAvailable()
    {
        var u = Make("X", 100, 1);
        u.RequiredGrandCompanyId = 1;
        u.RequiredGrandCompanyRank = 5;
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, playerGrandCompany: 1, playerGrandCompanyRank: 5);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void BeastTribe_RankTooLow_IsBeastTribeLocked()
    {
        var u = Make("X", 100, 1);
        u.RequiredBeastTribeId = 5;
        u.RequiredBeastTribeName = "Vanu Vanu";
        u.RequiredBeastTribeRank = 3;
        u.RequiredBeastTribeRankName = "Trusted";
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, getBeastTribeRank: id => id == 5 ? (byte)1 : (byte)0);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.BeastTribeLocked, all[0].Status);
        Assert.Equal("needs Vanu Vanu Trusted", all[0].LockReason);
    }

    [Fact]
    public void BeastTribe_RankMet_IsAvailable()
    {
        var u = Make("X", 100, 1);
        u.RequiredBeastTribeId = 5;
        u.RequiredBeastTribeRank = 3;
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, getBeastTribeRank: id => id == 5 ? (byte)3 : (byte)0);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void Mount_NotUnlocked_IsMountLocked()
    {
        var u = Make("X", 100, 1);
        u.RequiredMountId = 42;
        u.RequiredMountName = "Falcon";
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, isMountUnlocked: _ => false);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.MountLocked, all[0].Status);
        Assert.Equal("needs mount 'Falcon' unlocked", all[0].LockReason);
    }

    [Fact]
    public void Mount_Unlocked_IsAvailable()
    {
        var u = Make("X", 100, 1);
        u.RequiredMountId = 42;
        var all = new List<ResolvedUnlock> { u };

        var ctx = Gates.Ctx(playerLevel: 90, isMountUnlocked: id => id == 42);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void UnmodeledGate_IsUnknownGate_NeverAvailable()
    {
        var u = Make("X", 100, 1);
        u.HasUnmodeledGate = true;
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.UnknownGate, all[0].Status);
        Assert.NotNull(all[0].LockReason);
    }

    [Fact]
    public void Snapshot_IsIndependentOfLaterMutation()
    {
        var original = Make("X", 100, 10);
        original.Status = UnlockStatus.Available;
        var snapshot = original.Snapshot();
        original.Status = UnlockStatus.Done;
        original.LockReason = "mutated after snapshot";
        Assert.Equal(UnlockStatus.Available, snapshot.Status);
        Assert.Null(snapshot.LockReason);
    }

    [Fact]
    public void Parse_ReadsRealShape_AndDefaultsEnrichment()
    {
        const string json = """
        {"source":"s","fetched":"f","notes":"n","unlocks":[
          {"level":15,"unlock":"Glamours","type":"system","quest":"A Self-improving Man","questKind":"sidequest","notes":null,
           "description":"Change how gear looks.","priority":"essential","cosmetic":true},
          {"level":0,"unlock":"Old","type":"system","quest":null,"questKind":"sidequest","notes":"x"}
        ]}
        """;
        var defs = UnlockDataset.Parse(json);
        Assert.Equal(2, defs.Count);
        Assert.Equal("Glamours", defs[0].Unlock);
        Assert.Equal("essential", defs[0].Priority);
        Assert.True(defs[0].Cosmetic);
        Assert.Equal("nice", defs[1].Priority);   // enrichment default
        Assert.False(defs[1].Cosmetic);
        Assert.Null(defs[1].Quest);
        Assert.Null(defs[0].Requires);
        Assert.Equal("unverified", defs[1].Confidence);   // never assume a claim is backed
        Assert.Empty(defs[1].Sources);
    }

    /// <summary>One value of the wrong JSON kind takes the whole unlocks feature down, so the
    /// exception has to name the value that did it rather than leaving a maintainer to bisect a
    /// 586-entry catalogue against "the JSON value could not be converted".</summary>
    [Fact]
    public void Parse_WrongScalarType_ThrowsNamingTheOffendingPath()
    {
        const string json = """
        {"unlocks":[
          {"level":50,"unlock":"Firebird (Mount)","type":"mount","quest":"Fiery Wings, Fiery Hearts",
           "requires":{"items":[{"id":2002052,"name":"Firebird Whistle","keyItem":"yes"}]}}
        ]}
        """;
        var ex = Assert.Throws<JsonException>(() => UnlockDataset.Parse(json));
        Assert.Contains("unlocks dataset", ex.Message, StringComparison.Ordinal);
        Assert.Contains("keyItem", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsCuratedRequirementsAndProvenance()
    {
        const string json = """
        {"unlocks":[
          {"level":50,"unlock":"Firebird (Mount)","type":"mount","quest":"Fiery Wings, Fiery Hearts",
           "description":"A collectible mount.","priority":"optional","cosmetic":true,
           "requires":{"label":"all seven Heavensward Extreme-trial Lanner mounts",
                       "mounts":[{"id":76,"name":"Rose Lanner","from":"Thok ast Thok (Extreme)"}],
                       "minions":[{"id":1,"name":"Wind-up Kirin","from":null}],
                       "items":[{"id":2002052,"name":"Firebird Whistle","count":1,"keyItem":true}],
                       "jobs":[{"id":18,"name":"Fisher","level":61}],
                       "minLevel":80},
           "confidence":"single-source","sources":["gamerescape:progression-guide","game-data:Quest#67086"]}
        ]}
        """;
        var def = Assert.Single(UnlockDataset.Parse(json));
        Assert.NotNull(def.Requires);
        var req = def.Requires;
        Assert.Equal("all seven Heavensward Extreme-trial Lanner mounts", req.Label);
        Assert.False(req.Unverifiable);
        Assert.Equal(80, req.MinLevel);
        Assert.Equal(new UnlockRequirement.Collectible(76, "Rose Lanner", "Thok ast Thok (Extreme)"), Assert.Single(req.Mounts));
        Assert.Equal(1u, Assert.Single(req.Minions).Id);
        Assert.True(Assert.Single(req.Items).KeyItem);
        Assert.Equal(61, Assert.Single(req.Jobs).Level);
        Assert.True(req.HasCheckableRequirement);
        Assert.Equal("single-source", def.Confidence);
        Assert.Equal(2, def.Sources.Count);
    }

    [Fact]
    public void Parse_ReadsAnUnverifiableRequirement()
    {
        const string json = """
        {"unlocks":[
          {"level":70,"unlock":"Emanation (Extreme) Trial Access","type":"trial","quest":"Talk about Lakshmi",
           "description":"A single-boss fight for eight players.","priority":"nice","cosmetic":false,
           "requires":{"label":"unlocked by talking to an NPC, which leaves no record","unverifiable":true},
           "confidence":"unverified","sources":["gamerescape:progression-guide"]}
        ]}
        """;
        var def = Assert.Single(UnlockDataset.Parse(json));
        Assert.NotNull(def.Requires);
        var req = def.Requires;
        Assert.True(req.Unverifiable);
        Assert.False(req.HasCheckableRequirement);
        Assert.Empty(req.Mounts);
    }

    /// <summary>An entry as the catalogue ships it: an unlock name paired with the level the
    /// catalogue records, which together identify one tier of a progression.</summary>
    private static ResolvedUnlock Tier(string unlock, int level, uint? rowId) => new()
    {
        Def = new UnlockDefinition
        {
            Unlock = unlock,
            Level = level,
            Type = "system",
            Quest = rowId is null ? null : "q",
        },
        QuestRowId = rowId,
        QuestLevel = level,
    };

    private static ResolvedUnlock Make(string unlock, uint? rowId, int questLevel, params uint[] prereqs) => new()
    {
        Def = new UnlockDefinition { Unlock = unlock, Type = "system", Quest = rowId is null ? null : "q" },
        QuestRowId = rowId,
        QuestLevel = questLevel,
        PrereqRowIds = [.. prereqs],
        PrereqNames = [.. prereqs.Select(p => $"Quest {p}")],
    };
}
