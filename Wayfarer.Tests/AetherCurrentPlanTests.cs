using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>The aether-current route's own semantics, and above all the one number in it that could
/// be a lie: the per-zone total.</summary>
public class AetherCurrentPlanTests
{
    /// <summary>A real Coerthas Western Highlands placed current, as the base for the variations each
    /// test needs. Row 2818053 and set row 1 are the actual ids.</summary>
    private static readonly AetherCurrentPoint Blank =
        new(2818053, AetherCurrentKind.Attunable, 1, "Coerthas Western Highlands", 397, 211, 0f, 0f, 0f);

    /// <summary>The counts a set row actually holds, so the tally tests are about real shapes rather
    /// than invented ones: nine for every Heavensward-through-Endwalker zone, five for Azys Lla
    /// (all quest-granted, no placed currents at all), one for Mor Dhona, fifteen for each Dawntrail
    /// zone. Each is the number of NON-EMPTY entries in a column that is a fixed fifteen wide on
    /// every row — which is exactly why the column's length is not the denominator.</summary>
    public static TheoryData<int> RealZoneSizes => [9, 5, 1, 15];

    [Theory]
    [MemberData(nameof(RealZoneSizes))]
    public void TotalIsShownWhenOurCountAndTheGameAgreeThatWorkRemains(int known)
    {
        var tally = AetherCurrentPlan.Tally(known, attuned: 0, gameSaysZoneComplete: false);

        Assert.Equal(known, tally.Total);
        Assert.Equal(known, tally.Remaining);
        Assert.Equal(0, tally.Attuned);
    }

    [Theory]
    [MemberData(nameof(RealZoneSizes))]
    public void TotalIsShownWhenOurCountAndTheGameAgreeTheZoneIsFinished(int known)
    {
        var tally = AetherCurrentPlan.Tally(known, attuned: known, gameSaysZoneComplete: true);

        Assert.Equal(known, tally.Total);
        Assert.Equal(0, tally.Remaining);
    }

    /// <summary>The failure this whole design exists for. If the game says the zone is done while we
    /// still count currents outstanding, our list holds something the game does not require — so the
    /// denominator is wrong and is not printed. What IS still true is every individual bit we read,
    /// so the count of what is left survives.</summary>
    [Fact]
    public void TotalIsWithheldWhenTheGameSaysFinishedButWeStillCountSomeOutstanding()
    {
        var tally = AetherCurrentPlan.Tally(known: 10, attuned: 9, gameSaysZoneComplete: true);

        Assert.Null(tally.Total);
        Assert.Equal(1, tally.Remaining);
        Assert.Equal(9, tally.Attuned);
    }

    /// <summary>The mirror failure: we think the zone is finished and the game does not, so our list
    /// is MISSING a current. Same response — keep the counts, drop the claim.</summary>
    [Fact]
    public void TotalIsWithheldWhenWeThinkItIsFinishedAndTheGameDisagrees()
    {
        var tally = AetherCurrentPlan.Tally(known: 9, attuned: 9, gameSaysZoneComplete: false);

        Assert.Null(tally.Total);
        Assert.Equal(0, tally.Remaining);
        Assert.Equal(9, tally.Attuned);
    }

    [Fact]
    public void TotalIsWithheldWhenTheGameCouldNotBeAsked()
    {
        var tally = AetherCurrentPlan.Tally(known: 9, attuned: 4, gameSaysZoneComplete: null);

        Assert.Null(tally.Total);
        Assert.Equal(5, tally.Remaining);
    }

    [Fact]
    public void TotalIsWithheldForAZoneWeFoundNoCurrentsIn()
    {
        var tally = AetherCurrentPlan.Tally(known: 0, attuned: 0, gameSaysZoneComplete: true);

        Assert.Null(tally.Total);
        Assert.Equal(0, tally.Remaining);
    }

    /// <summary>More attuned than we found is another way of saying our list is short. It must not
    /// come out as a negative number of stops remaining.</summary>
    [Fact]
    public void RemainingNeverGoesNegative()
    {
        var tally = AetherCurrentPlan.Tally(known: 9, attuned: 12, gameSaysZoneComplete: true);

        Assert.Equal(0, tally.Remaining);
    }

    [Fact]
    public void ProgressTextClaimsATotalOnlyWhenThereIsOne()
    {
        Assert.Equal(
            "4 of 9 attuned",
            AetherCurrentPlan.ProgressText(AetherCurrentPlan.Tally(9, 4, gameSaysZoneComplete: false)));
        Assert.Equal(
            "5 left to attune",
            AetherCurrentPlan.ProgressText(AetherCurrentPlan.Tally(9, 4, gameSaysZoneComplete: null)));
    }

    [Fact]
    public void APlacedCurrentIsDoneOnlyWhenItIsAttuned()
    {
        Assert.False(AetherCurrentPlan.IsReached(
            AetherCurrentKind.Attunable, attuned: false, questAccepted: false, questComplete: false));
        Assert.True(AetherCurrentPlan.IsReached(
            AetherCurrentKind.Attunable, attuned: true, questAccepted: false, questComplete: false));
    }

    /// <summary>A placed current carries no quest, but nothing stops a caller passing the reads for
    /// one. Quest state must not be able to complete a stop the player has to fly to.</summary>
    [Fact]
    public void APlacedCurrentIsNotCompletedByQuestState()
    {
        Assert.False(AetherCurrentPlan.IsReached(
            AetherCurrentKind.Attunable, attuned: false, questAccepted: true, questComplete: true));
    }

    /// <summary>The route walks to the GIVER, so accepting the quest is the end of the walk. Holding
    /// the arrow there until the quest itself is finished would point at someone already spoken
    /// to.</summary>
    [Fact]
    public void AQuestCurrentIsDoneOnceTheQuestIsInHand()
    {
        Assert.False(AetherCurrentPlan.IsReached(
            AetherCurrentKind.Quest, attuned: false, questAccepted: false, questComplete: false));
        Assert.True(AetherCurrentPlan.IsReached(
            AetherCurrentKind.Quest, attuned: false, questAccepted: true, questComplete: false));
        Assert.True(AetherCurrentPlan.IsReached(
            AetherCurrentKind.Quest, attuned: false, questAccepted: false, questComplete: true));
        Assert.True(AetherCurrentPlan.IsReached(
            AetherCurrentKind.Quest, attuned: true, questAccepted: false, questComplete: false));
    }

    [Fact]
    public void APlacedCurrentRoutesToItsWorldPoint()
    {
        var destination = AetherCurrentPlan.Destination(Placed(397, 402f, 191.5f, 561.4f));

        var point = Assert.IsType<ObjectiveDestination.WorldPoint>(destination);
        Assert.Equal(397u, point.Territory);
        Assert.Equal(561.4f, point.Z);
    }

    /// <summary>A current with nowhere to go stays in the plan and says so, rather than being dropped
    /// (the plan would be quietly shorter than the zone) or pointed at the map's origin.</summary>
    [Fact]
    public void ACurrentWithNoLocationIsUnresolvedRatherThanDropped()
    {
        var placed = Assert.IsType<ObjectiveDestination.Unresolved>(
            AetherCurrentPlan.Destination(Placed(0, 0f, 0f, 0f)));
        Assert.Contains("where this current is", placed.Reason, StringComparison.Ordinal);

        var quest = Assert.IsType<ObjectiveDestination.Unresolved>(
            AetherCurrentPlan.Destination(Quest(0, 0f, 0f, "Baby Steps", "Voilinaut")));
        Assert.Contains("where this quest is given", quest.Reason, StringComparison.Ordinal);
    }

    /// <summary>The plate only ever carries words the game itself would print: the quest's name for a
    /// quest current, the game's own noun for a placed one.</summary>
    [Fact]
    public void TheHeadlineIsAlwaysTheGamesOwnWords()
    {
        Assert.Equal("Baby Steps", AetherCurrentPlan.Headline(Quest(397, 0f, 0f, "Baby Steps", "Voilinaut")));
        Assert.Equal("Aether Current", AetherCurrentPlan.Headline(Placed(397, 0f, 0f, 0f)));
    }

    [Fact]
    public void TheDetailSaysWhichKindOfStopThisIs()
    {
        Assert.Equal(
            "Speak with Voilinaut to earn this aether current",
            AetherCurrentPlan.Detail(Quest(397, 0f, 0f, "Baby Steps", "Voilinaut")));
        Assert.Equal(
            "Speak with the quest giver to earn this aether current",
            AetherCurrentPlan.Detail(Quest(397, 0f, 0f, "Baby Steps", giver: null)));
        Assert.Equal(
            "Fly here and attune to the aether current",
            AetherCurrentPlan.Detail(Placed(397, 0f, 0f, 0f)));
    }

    /// <summary>The heading is drawn in the game's display face, which does not carry the typographic
    /// apostrophe the zone name actually contains — the same lesson the hunting log's heading
    /// learned. "The Rak'tika Greatwood" must reach the plate as ASCII.</summary>
    [Fact]
    public void TheModeLabelIsFoldedToWhatTheHeadingFontCanDraw()
    {
        var label = AetherCurrentPlan.SourceLabel("The Rak’tika Greatwood");

        Assert.Equal("Aether Currents - The Rak'tika Greatwood", label);
        Assert.All(label, c => Assert.InRange(c, (char)0x20, (char)0x7E));
    }

    /// <summary>The readout's pill prints "Current " in front of the source name. Singular would make
    /// it stutter — "Current Aether Current" — so the plural is deliberate and pinned here, because
    /// it is the kind of wording a later tidy-up would "correct" back.</summary>
    [Fact]
    public void TheBannerPillDoesNotStutter()
    {
        Assert.Equal("Aether Currents", AetherCurrentPlan.SourceName);
        Assert.Equal(
            "Current Aether Currents",
            $"Current {Wayfarer.Core.Ui.DisplayNames.TitleCase(AetherCurrentPlan.SourceName)}");
    }

    [Fact]
    public void TheModeLabelStillNamesTheModeWithNoZoneName()
    {
        Assert.Equal("Aether Currents", AetherCurrentPlan.SourceLabel(null));
        Assert.Equal("Aether Currents", AetherCurrentPlan.SourceLabel(string.Empty));
    }

    private static AetherCurrentPoint Placed(uint territory, float x, float y, float z) =>
        Blank with { Territory = territory, X = x, Y = y, Z = z };

    private static AetherCurrentPoint Quest(uint territory, float x, float z, string quest, string? giver) =>
        Blank with
        {
            CurrentRowId = 2818051,
            Kind = AetherCurrentKind.Quest,
            Territory = territory,
            X = x,
            Z = z,
            QuestRowId = 67296,
            QuestName = quest,
            GiverName = giver,
        };
}
