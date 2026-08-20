using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

public class UnlockStatusTests
{
    [Fact]
    public void UnmatchedIsUnverified()
    {
        var all = new List<ResolvedUnlock> { Make("Mystery", null, 10) };
        UnlockStatusCalculator.Compute(all, 50, _ => false, _ => false);
        Assert.Equal(UnlockStatus.Unverified, all[0].Status);
    }

    [Fact]
    public void CompleteIsDone_EvenAboveLevel()
    {
        var all = new List<ResolvedUnlock> { Make("Glamours", 65754, 90) };
        UnlockStatusCalculator.Compute(all, 15, id => id == 65754, _ => false);
        Assert.Equal(UnlockStatus.Done, all[0].Status);
    }

    [Fact]
    public void AlternativeComplete_MarksAllDone()
    {
        var a = Make("Glamours", 100, 15);
        var b = Make("Glamours", 200, 15);
        var all = new List<ResolvedUnlock> { a, b };
        UnlockStatusCalculator.Compute(all, 20, id => id == 200, _ => false);
        Assert.Equal(UnlockStatus.Done, a.Status);
        Assert.Equal(UnlockStatus.Done, b.Status);
    }

    [Fact]
    public void AcceptedBeatsAvailable()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10) };
        UnlockStatusCalculator.Compute(all, 20, _ => false, id => id == 100);
        Assert.Equal(UnlockStatus.Accepted, all[0].Status);
    }

    [Fact]
    public void AboveLevel_IsLevelLocked_WithoutPrereqChecks()
    {
        var prereqChecked = false;
        var all = new List<ResolvedUnlock> { Make("X", 100, 60, 900) };
        UnlockStatusCalculator.Compute(
            all,
            20,
            id =>
            {
                if (id == 900)
                {
                    prereqChecked = true;
                }

                return false;
            },
            _ => false);
        Assert.Equal(UnlockStatus.LevelLocked, all[0].Status);
        Assert.Equal("needs level 60", all[0].LockReason);
        Assert.False(prereqChecked);
    }

    [Fact]
    public void IncompletePrereq_IsQuestLocked_WithName()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10, 900, 901) };
        UnlockStatusCalculator.Compute(all, 20, id => id == 900, _ => false);
        Assert.Equal(UnlockStatus.QuestLocked, all[0].Status);
        Assert.Equal("needs quest 'Quest 901'", all[0].LockReason);
    }

    [Fact]
    public void EverythingMet_IsAvailable()
    {
        var all = new List<ResolvedUnlock> { Make("X", 100, 10, 900) };
        UnlockStatusCalculator.Compute(all, 20, id => id == 900, _ => false);
        Assert.Equal(UnlockStatus.Available, all[0].Status);
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
        UnlockStatusCalculator.Compute(all, 50, _ => false, _ => false);
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

    private static ResolvedUnlock Make(string unlock, uint? rowId, int questLevel, params uint[] prereqs) => new()
    {
        Def = new UnlockDefinition { Unlock = unlock, Type = "system", Quest = rowId is null ? null : "q" },
        QuestRowId = rowId,
        QuestLevel = questLevel,
        PrereqRowIds = [.. prereqs],
        PrereqNames = [.. prereqs.Select(p => $"Quest {p}")],
    };
}
