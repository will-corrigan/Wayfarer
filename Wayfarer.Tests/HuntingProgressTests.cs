using Wayfarer.Core.Hunting;

namespace Wayfarer.Tests;

public class HuntingProgressTests
{
    [Theory]
    [InlineData(1, 3, HuntingPageState.Done)]
    [InlineData(2, 3, HuntingPageState.Done)]
    [InlineData(3, 3, HuntingPageState.Current)]
    [InlineData(4, 3, HuntingPageState.Locked)]
    [InlineData(5, 3, HuntingPageState.Locked)]
    public void PageState_TriState(int rank, int currentRank, HuntingPageState expected)
    {
        Assert.Equal(expected, HuntingProgress.PageState(rank, currentRank));
    }

    [Fact]
    public void RemainingForCurrentPage_ExcludesFullyKilledMonsters()
    {
        var rank = BuildRank();

        // task0/mon0 done (3/3), task1/mon0 in progress (2/5), task1/mon1 done (2/2).
        int Killed(int taskIndex, int monsterIndex) => (taskIndex, monsterIndex) switch
        {
            (0, 0) => 3,
            (1, 0) => 2,
            (1, 1) => 2,
            _ => 0,
        };

        var remaining = HuntingProgress.RemainingForCurrentPage(rank, Killed);

        var monster = Assert.Single(remaining);
        Assert.Equal(200u, monster.BNpcNameId);
    }

    [Fact]
    public void RemainingForCurrentPage_NoneKilled_ReturnsAllInPositionalOrder()
    {
        var rank = BuildRank();

        var remaining = HuntingProgress.RemainingForCurrentPage(rank, (_, _) => 0);

        Assert.Equal([100u, 200u, 201u], remaining.Select(m => m.BNpcNameId));
    }

    [Fact]
    public void RemainingForCurrentPage_AllKilled_ReturnsEmpty()
    {
        var rank = BuildRank();

        var remaining = HuntingProgress.RemainingForCurrentPage(rank, (_, _) => 999);

        Assert.Empty(remaining);
    }

    private static HuntingRank BuildRank()
    {
        return new HuntingRank
        {
            Rank = 3,
            Tasks =
            [
                new HuntingTask
                {
                    TaskIndex = 0,
                    Monsters =
                    [
                        new HuntingMonster { MonsterIndex = 0, BNpcNameId = 100, RequiredKills = 3 },
                    ],
                },
                new HuntingTask
                {
                    TaskIndex = 1,
                    Monsters =
                    [
                        new HuntingMonster { MonsterIndex = 0, BNpcNameId = 200, RequiredKills = 5 },
                        new HuntingMonster { MonsterIndex = 1, BNpcNameId = 201, RequiredKills = 2 },
                    ],
                },
            ],
        };
    }
}
