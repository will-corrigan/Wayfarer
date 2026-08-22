using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

public class GuidanceChainTests
{
    [Fact]
    public void UnlockRoutePlan_AdvancesWhenQuestAccepted()
    {
        var accepted = new HashSet<uint>();
        var chain = new GuidanceChain<Pickup>(
            [new Pickup(1), new Pickup(2), new Pickup(3)],
            p => UnlockRoutePlan.IsPickedUp(accepted.Contains(p.QuestRowId), questComplete: false));

        Assert.Equal(new Pickup(1), chain.Advance());
        Assert.Equal(1, chain.Index);
        Assert.Equal(3, chain.Total);

        accepted.Add(1);

        Assert.Equal(new Pickup(2), chain.Advance());
        Assert.Equal(2, chain.Index);
    }

    [Fact]
    public void UnlockRoutePlan_SkipsAlreadyCompleteStops()
    {
        var chain = new GuidanceChain<Pickup>(
            [new Pickup(1), new Pickup(2), new Pickup(3)],
            p => UnlockRoutePlan.IsPickedUp(questAccepted: false, questComplete: p.QuestRowId < 3));

        Assert.Equal(new Pickup(3), chain.Advance());
        Assert.Equal(3, chain.Index);
    }

    [Fact]
    public void UnlockRoutePlan_ExhaustedReturnsNull()
    {
        var chain = new GuidanceChain<Pickup>([new Pickup(1)], _ => true);

        Assert.Null(chain.Advance());
        Assert.Null(chain.Current);
    }

    [Fact]
    public void HuntingPlan_BelowRequiredKills_KeepsSameLeg()
    {
        var killed = 0;
        var chain = new GuidanceChain<Target>(
            [new Target("Ornery Karakul", 3), new Target("Wild Dodo", 3)],
            t => HuntingPlan.IsComplete(killed, t.Required));

        Assert.Equal("Ornery Karakul", chain.Advance()!.Name);
        killed = 2;
        Assert.Equal("Ornery Karakul", chain.Advance()!.Name);
        Assert.Equal("2/3", HuntingPlan.ProgressText(killed, 3));
    }

    [Fact]
    public void HuntingPlan_ReachingRequiredKills_AdvancesToNextLeg()
    {
        var killed = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 0, ["b"] = 0 };
        var chain = new GuidanceChain<Target>(
            [new Target("a", 3), new Target("b", 3)],
            t => HuntingPlan.IsComplete(killed[t.Name], t.Required));

        Assert.Equal("a", chain.Advance()!.Name);
        killed["a"] = 3;
        Assert.Equal("b", chain.Advance()!.Name);
        Assert.Equal(2, chain.Index);
    }

    [Fact]
    public void HuntingPlan_LastTargetComplete_ReturnsNull()
    {
        var killed = 0;
        var chain = new GuidanceChain<Target>([new Target("a", 1)], t => HuntingPlan.IsComplete(killed, t.Required));

        Assert.NotNull(chain.Advance());
        killed = 1;
        Assert.Null(chain.Advance());
    }

    /// <summary>A burst of progress (several kills landing between two polls) must skip every
    /// completed leg in ONE advance, not one per tick — otherwise the arrow walks the player
    /// through targets they already finished.</summary>
    [Fact]
    public void Chain_Advance_SkipsMultipleAlreadyCompleteLegs()
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        var chain = new GuidanceChain<Target>(
            [new Target("a", 1), new Target("b", 1), new Target("c", 1), new Target("d", 1)],
            t => done.Contains(t.Name));

        Assert.Equal("a", chain.Advance()!.Name);
        done.Add("a");
        done.Add("b");
        done.Add("c");

        Assert.Equal("d", chain.Advance()!.Name);
        Assert.Equal(4, chain.Index);
    }

    /// <summary>The "arrow must not jump mid-approach" rule: a re-plan triggered by the player
    /// turning up somewhere unexpected re-orders where they go NEXT, never where they are already
    /// heading.</summary>
    [Fact]
    public void Chain_ReplanTail_NeverChangesTheCurrentLeg()
    {
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var chain = new GuidanceChain<Target>(
            [new Target("a", 1), new Target("b", 1), new Target("c", 1), new Target("d", 1)],
            t => completed.Contains(t.Name));
        chain.Advance();

        chain.ReplanTail(tail => [.. tail.Reverse()]);

        Assert.Equal("a", chain.Current!.Name); // head pinned
        Assert.Equal(4, chain.Total);

        var visited = new List<string> { chain.Current.Name };
        completed.Add("a");
        visited.Add(chain.Advance()!.Name);
        completed.Add("d");
        visited.Add(chain.Advance()!.Name);

        Assert.Equal(["a", "d", "c"], visited);
    }

    [Fact]
    public void Chain_ReplanTail_OnTheLastLeg_IsANoOp()
    {
        var chain = new GuidanceChain<Target>([new Target("a", 1)], _ => false);
        chain.Advance();

        chain.ReplanTail(_ => [new Target("z", 1)]);

        Assert.Equal("a", chain.Current!.Name);
        Assert.Equal(1, chain.Total);
    }

    /// <summary>An unlock-route leg: a quest row the player either has or hasn't accepted.</summary>
    private sealed record Pickup(uint QuestRowId);

    /// <summary>A hunting leg: a monster with a required kill count.</summary>
    private sealed record Target(string Name, int Required);
}
