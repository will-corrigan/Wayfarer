using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The route cap, and the rule that matters more than the number: <b>it is never
/// silent.</b> A plan that quietly stops after eight stops reads as "that was everything", which is
/// the class of dishonesty this project keeps removing.</summary>
public class UnlockRouteCapTests
{
    /// <summary>The behaviour at the boundary, which is where a cap is either honest or not.
    ///
    /// <para>At and below the cap the plan IS everything, so the label is the plain count — inventing
    /// "next 8 of 8" would announce a cap the player has not hit. One past it, both numbers appear.
    /// The transition is at 8/9 and it is asserted from both sides.</para></summary>
    [Theory]
    [InlineData(0, "Route Me")]
    [InlineData(1, "Route Me (1)")]
    [InlineData(7, "Route Me (7)")]
    [InlineData(8, "Route Me (8)")]
    [InlineData(9, "Route: next 8 of 9")]
    [InlineData(47, "Route: next 8 of 47")]
    [InlineData(510, "Route: next 8 of 510")]
    public void TheButtonStatesTheCapWheneverThereIsOne(int total, string expected)
    {
        Assert.Equal(expected, UnlockRouteCap.ButtonLabel(total));
    }

    /// <summary>The spec's own example, verbatim. Pinned as a literal because the words are the
    /// feature: a reader of this test should be able to see the exact string a player sees.</summary>
    [Fact]
    public void TheLabelReadsRouteNextEightOfFortySeven()
    {
        Assert.Equal("Route: next 8 of 47", UnlockRouteCap.ButtonLabel(47));
    }

    /// <summary>Whenever the plan leaves anything out, the label carries BOTH numbers. This is the
    /// assertion the whole cap rests on, made over the range rather than at a sample: there is no
    /// total at which the button shows one number while the plan walks another.</summary>
    [Fact]
    public void NoTotalProducesALabelThatHidesATruncation()
    {
        for (var total = 0; total <= 200; total++)
        {
            var label = UnlockRouteCap.ButtonLabel(total);
            var taken = UnlockRouteCap.Take(total);

            Assert.Equal(taken < total, UnlockRouteCap.Truncates(total));

            if (taken < total)
            {
                Assert.Contains(
                    taken.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    label,
                    StringComparison.Ordinal);
                Assert.Contains(
                    total.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    label,
                    StringComparison.Ordinal);
            }
        }
    }

    /// <summary>The caption beside the follow-list row carries the same two numbers in the width a
    /// trailing caption has, so starting a route from there cannot show a different figure from
    /// starting it from the button.</summary>
    [Theory]
    [InlineData(0, "")]
    [InlineData(8, "8")]
    [InlineData(47, "8 of 47")]
    public void TheFollowListCaptionAgreesWithTheButton(int total, string expected)
    {
        Assert.Equal(expected, UnlockRouteCap.Caption(total));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(8, 8)]
    [InlineData(9, 8)]
    [InlineData(1208, 8)]
    public void TakeNeverExceedsTheCapAndNeverExceedsWhatThereIs(int total, int expected)
    {
        Assert.Equal(expected, UnlockRouteCap.Take(total));
    }

    /// <summary>The planner applies the cap; the orderer does not. Both halves matter: the three
    /// Route Me surfaces all call <c>Plan</c> so none of them can be uncapped by omission, and
    /// <c>Order</c> stays whole so a caller that wants the full ordering is not fighting a cap.</summary>
    [Fact]
    public void PlanCapsAndOrderDoesNot()
    {
        var pool = Enumerable.Range(0, 30)
            .Select(i => Stop($"Stop {i}", x: i * 10f))
            .ToList();

        var ordered = RoutePlanner.Order([.. pool], currentTerritory: 132, px: 0f, pz: 0f);
        var planned = RoutePlanner.Plan([.. pool], currentTerritory: 132, px: 0f, pz: 0f);

        Assert.Equal(30, ordered.Count);
        Assert.Equal(UnlockRouteCap.Stops, planned.Count);

        // And the plan is the ordering's own prefix, not a different eight: the nearest eight, in the
        // order the uncapped route would have walked them.
        Assert.Equal<object>(
            ordered.Take(UnlockRouteCap.Stops).Select(u => u.Def.Unlock),
            planned.Select(u => u.Def.Unlock));
    }

    /// <summary>Under the cap, the plan is the whole ordering — the cap costs nothing when it does
    /// not bite.</summary>
    [Fact]
    public void APlanUnderTheCapIsTheWholeOrdering()
    {
        var pool = Enumerable.Range(0, 5).Select(i => Stop($"Stop {i}", x: i * 10f)).ToList();

        var ordered = RoutePlanner.Order([.. pool], currentTerritory: 132, px: 0f, pz: 0f);
        var planned = RoutePlanner.Plan([.. pool], currentTerritory: 132, px: 0f, pz: 0f);

        Assert.Equal<object>(ordered.Select(u => u.Def.Unlock), planned.Select(u => u.Def.Unlock));
    }

    /// <summary>The sentence-length form says what will be walked and what will not, and says
    /// something true either way.</summary>
    [Fact]
    public void TheExplanationSaysWhatIsLeftOut()
    {
        Assert.Contains("of 47", UnlockRouteCap.Explanation(47), StringComparison.Ordinal);
        Assert.Contains("Press again", UnlockRouteCap.Explanation(47), StringComparison.Ordinal);
        Assert.DoesNotContain("of 4", UnlockRouteCap.Explanation(4), StringComparison.Ordinal);
    }

    private static ResolvedUnlock Stop(string name, float x) => new()
    {
        Def = new UnlockDefinition { Unlock = name, Channel = "system", Type = "system" },
        QuestRowId = 1,
        QuestLevel = 10,
        GiverTerritory = 132,
        GiverX = x,
        GiverZ = 0f,
        Status = UnlockStatus.Available,
    };
}
