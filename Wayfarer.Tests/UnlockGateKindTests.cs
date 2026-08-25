using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The three gate kinds the catalogue recovery introduced, and the rule they all share.
///
/// <para>The recovery re-derived 89 entries the catalogue could say nothing about, and the useful
/// result is not that they became <i>available</i> — most did not, and could not honestly. It is
/// that "status unknown" turned into a specific, checkable reason: clear this duty, carry this
/// map, finish any one of these three quests. A gate that is satisfied still never yields
/// Available on its own, because for these entries the last step (talking to the Wandering
/// Minstrel, using the map) leaves no record a plugin can read.</para></summary>
public class UnlockGateKindTests
{
    private const uint SigmascapeInstanceContentId = 30066;
    private const uint DragonskinMapItemId = 12243;

    [Fact]
    public void DutyGate_NotCleared_IsInstanceLockedAndNamesTheDuty()
    {
        var all = new List<ResolvedUnlock> { DutyGated() };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 100));

        Assert.Equal(UnlockStatus.InstanceLocked, all[0].Status);
        Assert.Equal("requires clearing Sigmascape V4.0 (Savage)", all[0].LockReason);
        Assert.Equal("Sigmascape V4.0 (Savage)", Assert.Single(all[0].MissingRequirements));
    }

    /// <summary>Clearing the prerequisite opens the door. Whether the player has walked through it
    /// is the part the client does not record, so this stops at "unknown" rather than sending
    /// someone to unlock something they unlocked months ago.</summary>
    [Fact]
    public void DutyGate_Cleared_IsStillNotAvailable()
    {
        var all = new List<ResolvedUnlock> { DutyGated() };
        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(playerLevel: 100, isInstanceContentCompleted: id => id == SigmascapeInstanceContentId));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);

        // The hedge lives in the sentence the pane draws, not in the reason: "Requirements unknown —
        // {reason}." The reason's job is to name the requirement, once.
        Assert.Contains("Sigmascape V4.0 (Savage)", all[0].LockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemGate_WithoutTheMap_NamesTheMap()
    {
        var all = new List<ResolvedUnlock> { ItemGated() };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 100));

        Assert.Equal(UnlockStatus.CollectionLocked, all[0].Status);
        Assert.Contains("Timeworn Dragonskin Map", all[0].LockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemGate_WithTheMap_IsStillNotAvailable()
    {
        var all = new List<ResolvedUnlock> { ItemGated() };
        UnlockStatusCalculator.Compute(
            all,
            Gates.Ctx(playerLevel: 100, getOwnedItemCount: id => id == DragonskinMapItemId ? 1 : 0));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);
    }

    /// <summary>The guard the whole quest-less branch is built around: an entry with nothing to
    /// check stays exactly where it was.</summary>
    [Fact]
    public void NoQuestAndNothingCheckable_IsStillUnverified()
    {
        var all = new List<ResolvedUnlock>
        {
            new()
            {
                Def = new UnlockDefinition
                {
                    Unlock = "Hunting Logs",
                    Type = "system",
                    Level = 1,
                    Requires = new UnlockRequirement { Label = "completion of a level 1 class quest", Unverifiable = true },
                },
            },
        };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 50));
        Assert.Equal(UnlockStatus.Unverified, all[0].Status);
    }

    /// <summary>The recovery's largest honest win: a character who did the Maelstrom version of a
    /// three-way Grand Company quest is Done, where the old name match bound one row arbitrarily
    /// and told two thirds of characters they had not started.</summary>
    [Fact]
    public void QuestAnyOf_AnyOneComplete_IsDone()
    {
        var all = new List<ResolvedUnlock> { GrandCompanySelection() };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 30, isQuestComplete: id => id == 66217));

        Assert.Equal(UnlockStatus.Done, all[0].Status);
    }

    [Fact]
    public void QuestAnyOf_NoneComplete_SaysItCannotTellWhichIsYours()
    {
        var all = new List<ResolvedUnlock> { GrandCompanySelection() };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 30));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);
        Assert.Contains("quests share this name", all[0].LockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsTheNewGateKinds()
    {
        const string json = """
        {"unlocks":[
          {"level":20,"unlock":"Grand Company Selection","type":"system","quest":"The Company You Keep",
           "questAnyOf":[66216,66217,66218],
           "description":"Join one of the three Grand Companies.","priority":"essential","cosmetic":false,
           "confidence":"verified","sources":["gamerescape:progression-guide","game-data:Quest#66216"]},
          {"level":70,"unlock":"The Weapon's Refrain (Ultimate) Access","type":"raid","quest":null,
           "description":"An eight-player Ultimate raid.","priority":"optional","cosmetic":false,
           "requires":{"label":"unlocked by clearing Sigmascape V4.0 (Savage)",
                       "duties":[{"id":30066,"name":"Sigmascape V4.0 (Savage)"}],
                       "unverifiable":true},
           "confidence":"unverified","sources":["gamerescape:progression-guide"]}
        ]}
        """;

        var defs = UnlockDataset.Parse(json);
        Assert.Equal([66216u, 66217u, 66218u], defs[0].QuestAnyOf);
        Assert.Empty(defs[1].QuestAnyOf);

        var duty = Assert.Single(defs[1].Requires!.Duties);
        Assert.Equal(SigmascapeInstanceContentId, duty.Id);
        Assert.Equal("Sigmascape V4.0 (Savage)", duty.Name);
        Assert.True(defs[1].Requires!.HasCheckableRequirement);
    }

    private static ResolvedUnlock DutyGated() => new()
    {
        Def = new UnlockDefinition
        {
            Unlock = "The Weapon's Refrain (Ultimate) Access",
            Type = "raid",
            Level = 70,
            Requires = new UnlockRequirement
            {
                Label = "unlocked by clearing Sigmascape V4.0 (Savage)",
                Duties = [new UnlockRequirement.Collectible(SigmascapeInstanceContentId, "Sigmascape V4.0 (Savage)", null)],
                Unverifiable = true,
            },
        },
    };

    private static ResolvedUnlock ItemGated() => new()
    {
        Def = new UnlockDefinition
        {
            Unlock = "The Aquapolis Access",
            Type = "system",
            Level = 60,
            Requires = new UnlockRequirement
            {
                Label = "entered with a Timeworn Dragonskin Map, never from a quest",
                Items = [new UnlockRequirement.RequiredItem(DragonskinMapItemId, "Timeworn Dragonskin Map", 1, false)],
                Unverifiable = true,
            },
        },
    };

    private static ResolvedUnlock GrandCompanySelection() => new()
    {
        Def = new UnlockDefinition
        {
            Unlock = "Grand Company Selection",
            Type = "system",
            Level = 20,
            Quest = "The Company You Keep",
            QuestAnyOf = [66216, 66217, 66218],
        },
        QuestRowId = 66216,
        AlternativeQuestRowIds = [66216, 66217, 66218],
        QuestLevel = 20,
    };
}
