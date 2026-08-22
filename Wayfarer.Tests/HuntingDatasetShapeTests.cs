using Wayfarer.Core.Hunting;

namespace Wayfarer.Tests;

/// <summary>Loads the real shipped <c>data/hunting-targets.json</c> (copied into the test output
/// directory — see Wayfarer.Tests.csproj) through <see cref="HuntingDataset.Parse"/> and asserts
/// the same totals as <c>data/validate-hunting-targets.mjs</c>, so a schema/data regression is
/// caught by <c>dotnet test</c> too, not only the Node validator.</summary>
public class HuntingDatasetShapeTests
{
    private static readonly string[] ExpectedLogKeys =
        ["1", "2", "3", "4", "5", "6", "7", "26", "29", "10001", "10002", "10003"];

    [Fact]
    public void Logs_HasExactlyTwelveExpectedKeys()
    {
        var d = Load();
        Assert.Equal(ExpectedLogKeys.Length, d.Logs.Count);
        foreach (var key in ExpectedLogKeys)
        {
            Assert.True(d.Logs.ContainsKey(key), $"missing log key {key}");
        }
    }

    [Theory]
    [InlineData("1", "classJob", 5)]
    [InlineData("29", "classJob", 5)]
    [InlineData("10001", "grandCompanyElite", 3)]
    [InlineData("10002", "grandCompanyElite", 3)]
    [InlineData("10003", "grandCompanyElite", 3)]
    public void Log_KindAndRankCount(string key, string expectedKind, int expectedRankCount)
    {
        var d = Load();
        var log = d.Logs[key];
        Assert.Equal(expectedKind, log.Kind);
        Assert.Equal(expectedRankCount, log.Ranks.Count);
    }

    [Fact]
    public void EveryRank_HasExactlyTenTasks()
    {
        var d = Load();
        foreach (var (key, log) in d.Logs)
        {
            foreach (var rank in log.Ranks)
            {
                Assert.True(rank.Tasks.Count == 10, $"log {key} rank {rank.Rank}: expected 10 tasks, got {rank.Tasks.Count}");
            }
        }
    }

    [Fact]
    public void Totals_MatchPinnedRegressionCounts()
    {
        var d = Load();

        var totalTasks = 0;
        var totalMonsterRecords = 0;
        var routableFalseCount = 0;
        var bNpcNameIds = new HashSet<uint>();

        foreach (var log in d.Logs.Values)
        {
            foreach (var rank in log.Ranks)
            {
                foreach (var task in rank.Tasks)
                {
                    totalTasks++;
                    foreach (var monster in task.Monsters)
                    {
                        totalMonsterRecords++;
                        bNpcNameIds.Add(monster.BNpcNameId);
                        foreach (var loc in monster.Locations)
                        {
                            if (!loc.Routable)
                            {
                                routableFalseCount++;
                            }
                        }
                    }
                }
            }
        }

        Assert.Equal(540, totalTasks);
        Assert.Equal(666, totalMonsterRecords);

        // 362, not the prep report's 361: 6 records had a same-display-name duplicate-row-id
        // curation slip fixed against live MonsterNote/MonsterNoteTarget sheet data 2026-08-22
        // (see data/validate-hunting-targets.mjs for the same pinned constant).
        Assert.Equal(362, bNpcNameIds.Count);
        Assert.Equal(25, routableFalseCount);
    }

    [Fact]
    public void EveryMonster_HasExactlyOnePrimaryLocationAtIndexZero()
    {
        var d = Load();
        foreach (var log in d.Logs.Values)
        {
            foreach (var rank in log.Ranks)
            {
                foreach (var task in rank.Tasks)
                {
                    foreach (var monster in task.Monsters)
                    {
                        Assert.NotEmpty(monster.Locations);
                        Assert.True(monster.Locations[0].IsPrimary);
                        Assert.Equal(1, monster.Locations.Count(l => l.IsPrimary));
                    }
                }
            }
        }
    }

    [Fact]
    public void EveryNonRoutableLocation_HasDutyTerritoryTypeId()
    {
        var d = Load();
        foreach (var log in d.Logs.Values)
        {
            foreach (var rank in log.Ranks)
            {
                foreach (var task in rank.Tasks)
                {
                    foreach (var monster in task.Monsters)
                    {
                        foreach (var loc in monster.Locations)
                        {
                            if (!loc.Routable)
                            {
                                Assert.NotNull(loc.DutyTerritoryTypeId);
                            }
                        }
                    }
                }
            }
        }
    }

    // Regression test for the controller-wave job-key bug (HuntingLogService.cs:311): every
    // ClassJobId HuntingSlotTable.SlotForClassJob resolves a slot for — base classes AND the ten
    // evolved jobs — must resolve to a real dataset key once run through
    // HuntingSlotTable.BaseClassFor, the single source of truth HuntingLogService.ResolveActiveLog
    // must (and now does) use to build its jobKey. Covers 1-43 (every defined ClassJobId) so a
    // future evolved job added to EvolvedToBaseClass without a matching dataset key fails here
    // instead of silently reproducing the "Hunting log data missing for this job." bug in-game.
    [Fact]
    public void EveryClassLogClassJobId_ResolvesViaBaseClassFor_ToADatasetKey()
    {
        var d = Load();
        for (uint classJobId = 1; classJobId <= 43; classJobId++)
        {
            if (HuntingSlotTable.SlotForClassJob(classJobId) is null)
            {
                continue; // post-Stormblood job, or unknown id — no class log, nothing to resolve
            }

            var jobKey = HuntingSlotTable.BaseClassFor(classJobId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(d.Logs.ContainsKey(jobKey), $"classJobId {classJobId} -> jobKey {jobKey} missing from dataset");
        }
    }

    private static HuntingDataset Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "hunting-targets.json");
        return HuntingDataset.Parse(File.ReadAllText(path));
    }
}
