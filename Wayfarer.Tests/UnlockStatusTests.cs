using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

public class UnlockStatusTests
{
    private const uint WeaverJobRowId = 44;
    private const uint CulinarianJobRowId = 45;
    private const uint DungeonInstanceContentId = 3000;

    [Fact]
    public void UnmatchedIsUnverified()
    {
        var all = new List<ResolvedUnlock> { Make("Mystery", null, 10) };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 50));
        Assert.Equal(UnlockStatus.Unverified, all[0].Status);
    }

    [Fact]
    public void CompleteIsDone_EvenAboveLevel()
    {
        var all = new List<ResolvedUnlock> { Make("Glamours", 65754, 90) };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 15, isQuestComplete: id => id == 65754));
        Assert.Equal(UnlockStatus.Done, all[0].Status);
    }

    [Fact]
    public void AlternativeComplete_MarksAllDone()
    {
        var a = Make("Glamours", 100, 15);
        var b = Make("Glamours", 200, 15);
        var all = new List<ResolvedUnlock> { a, b };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20, isQuestComplete: id => id == 200));
        Assert.Equal(UnlockStatus.Done, a.Status);
        Assert.Equal(UnlockStatus.Done, b.Status);
    }

    [Fact]
    public void AcceptedBeatsAvailable()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10) };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20, isQuestAccepted: id => id == 100));
        Assert.Equal(UnlockStatus.Accepted, all[0].Status);
    }

    [Fact]
    public void AboveLevel_IsLevelLocked_WithoutPrereqChecks()
    {
        var prereqChecked = false;
        var all = new List<ResolvedUnlock> { Make("X", 100, 60, 900) };
        UnlockStatusCalculator.Compute(
            all,
            Ctx(
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
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20, isQuestComplete: id => id == 900));
        Assert.Equal(UnlockStatus.QuestLocked, all[0].Status);
        Assert.Equal("needs quest 'Quest 901'", all[0].LockReason);
    }

    [Fact]
    public void PrereqJoinOr_AnyOneComplete_Unblocks()
    {
        var u = Make("X", 100, 10, 900, 901);
        u.PrereqJoin = 2;
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20, isQuestComplete: id => id == 901));
        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void PrereqJoinOr_NoneComplete_QuestLocked_NamesBoth()
    {
        var u = Make("X", 100, 10, 900, 901);
        u.PrereqJoin = 2;
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20));
        Assert.Equal(UnlockStatus.QuestLocked, all[0].Status);
        Assert.Equal("needs quest 'Quest 900' or 'Quest 901'", all[0].LockReason);
    }

    [Fact]
    public void EverythingMet_IsAvailable()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10, 900) };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20, isQuestComplete: id => id == 900));
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
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20, isQuestComplete: id => id == 701));
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
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 1, isQuestComplete: id => id == 700));
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
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 20));
        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void ClassJobCategory_RowZero_IsUnrestricted_UsesActiveJobLevel()
    {
        var u = Make("X", 100, 50);
        var all = new List<ResolvedUnlock> { u };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 55));
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

        var ctx = Ctx(
            playerLevel: 90,
            getClassJobLevel: jobId => jobId == WeaverJobRowId ? 1 : 0);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
        Assert.Equal("needs Weaver 50", all[0].LockReason);
    }

    [Fact]
    public void MultiJobCategory_UsesMaxLevelAmongFlaggedJobs()
    {
        var u = Make("X", 100, 50);
        u.RequiredJobRowIds = [WeaverJobRowId, CulinarianJobRowId];
        u.RequiredJobNames = ["Weaver", "Culinarian"];
        var all = new List<ResolvedUnlock> { u };

        var ctx = Ctx(
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

        var ctx = Ctx(playerLevel: 90, getClassJobLevel: _ => 5);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
        Assert.Equal("needs Weaver or Culinarian 50", all[0].LockReason);
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

        var ctx = Ctx(
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

        var ctx = Ctx(playerLevel: 90, isInstanceContentUnlocked: _ => false, isInstanceContentCompleted: _ => false);
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

        var ctx = Ctx(playerLevel: 90, isInstanceContentCompleted: id => id == 1, isInstanceContentUnlocked: _ => true);
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

        var ctx = Ctx(playerLevel: 90, isInstanceContentCompleted: id => id == 1, isInstanceContentUnlocked: _ => true);
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

        var ctx = Ctx(playerLevel: 90, playerGrandCompany: 1);
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

        var ctx = Ctx(playerLevel: 90, playerGrandCompany: 1, playerGrandCompanyRank: 2);
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

        var ctx = Ctx(playerLevel: 90, playerGrandCompany: 1, playerGrandCompanyRank: 5);
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

        var ctx = Ctx(playerLevel: 90, getBeastTribeRank: id => id == 5 ? (byte)1 : (byte)0);
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

        var ctx = Ctx(playerLevel: 90, getBeastTribeRank: id => id == 5 ? (byte)3 : (byte)0);
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

        var ctx = Ctx(playerLevel: 90, isMountUnlocked: _ => false);
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

        var ctx = Ctx(playerLevel: 90, isMountUnlocked: id => id == 42);
        UnlockStatusCalculator.Compute(all, ctx);

        Assert.Equal(UnlockStatus.Available, all[0].Status);
    }

    [Fact]
    public void UnmodeledGate_IsUnknownGate_NeverAvailable()
    {
        var u = Make("X", 100, 1);
        u.HasUnmodeledGate = true;
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.UnknownGate, all[0].Status);
        Assert.NotNull(all[0].LockReason);
    }

    [Fact]
    public void CountAvailableIn_FiltersByTerritory()
    {
        var a = Make("A", 1, 1);
        a.GiverTerritory = 132;
        var b = Make("B", 2, 1);
        b.GiverTerritory = 130;
        var c = Make("C", 3, 1);
        c.GiverTerritory = 132;
        var all = new List<ResolvedUnlock> { a, b, c };
        UnlockStatusCalculator.Compute(all, Ctx(playerLevel: 50));
        Assert.Equal(2, UnlockStatusCalculator.CountAvailableIn(all, 132));
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
    }

    private static UnlockGateContext Ctx(
        int playerLevel,
        byte playerGrandCompany = 0,
        int playerGrandCompanyRank = 0,
        Func<uint, bool>? isQuestComplete = null,
        Func<uint, bool>? isQuestAccepted = null,
        Func<uint, int>? getClassJobLevel = null,
        Func<uint, bool>? isInstanceContentCompleted = null,
        Func<uint, bool>? isInstanceContentUnlocked = null,
        Func<byte, byte>? getBeastTribeRank = null,
        Func<uint, bool>? isMountUnlocked = null) => new(
            playerLevel,
            playerGrandCompany,
            playerGrandCompanyRank,
            isQuestComplete ?? (_ => false),
            isQuestAccepted ?? (_ => false),
            getClassJobLevel ?? (_ => 0),
            isInstanceContentCompleted ?? (_ => false),
            isInstanceContentUnlocked ?? (_ => true),
            getBeastTribeRank ?? (_ => 0),
            isMountUnlocked ?? (_ => false));

    private static ResolvedUnlock Make(string unlock, uint? rowId, int questLevel, params uint[] prereqs) => new()
    {
        Def = new UnlockDefinition { Unlock = unlock, Type = "system", Quest = rowId is null ? null : "q" },
        QuestRowId = rowId,
        QuestLevel = questLevel,
        PrereqRowIds = [.. prereqs],
        PrereqNames = [.. prereqs.Select(p => $"Quest {p}")],
    };
}
