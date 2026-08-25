using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The three bands and the order they appear in, and the sectioning that puts them inside a
/// group. The claims here are the ones the windows cannot make for themselves: both link against the
/// game, so "Available comes before Blocked" was previously only true because two independent sort
/// expressions happened to agree.</summary>
public class UnlockBandTests
{
    private static readonly UnlockBand[] ExpectedOrder =
        [UnlockBand.Available, UnlockBand.Blocked, UnlockBand.NotKnown, UnlockBand.Complete];

    private static readonly UnlockBand[] AvailableBlockedNotKnown =
        [UnlockBand.Available, UnlockBand.Blocked, UnlockBand.NotKnown];

    private static readonly UnlockBand[] AvailableThenBlocked = [UnlockBand.Available, UnlockBand.Blocked];

    private static readonly string[] DeltaBetaZeta = ["Delta", "Beta", "Zeta"];

    private static readonly string[] LevelledThenNot = ["Level 90", "No level"];

    private static readonly string[] DutiesCapabilitiesTitles = ["Duties", "Capabilities", "Titles"];

    /// <summary>Available, then Blocked, then Not known. The order is what the whole band idea is
    /// for — it is the order the player can act on — so it is pinned rather than left to the enum's
    /// declaration order to imply.</summary>
    [Fact]
    public void TheBandsRunAvailableThenBlockedThenNotKnown()
    {
        Assert.Equal(ExpectedOrder, UnlockBands.All);

        Assert.True(UnlockBands.Rank(UnlockBand.Available) < UnlockBands.Rank(UnlockBand.Blocked));
        Assert.True(UnlockBands.Rank(UnlockBand.Blocked) < UnlockBands.Rank(UnlockBand.NotKnown));
        Assert.True(UnlockBands.Rank(UnlockBand.NotKnown) < UnlockBands.Rank(UnlockBand.Complete));
    }

    /// <summary>The "Not known" band says so, in those words. It is the band whose whole purpose is
    /// to be labelled — an unlabelled one is indistinguishable from a list of locked entries — so the
    /// label is asserted literally rather than "is not empty".</summary>
    [Fact]
    public void TheUnknownBandIsLabelledAsUnknownRatherThanAsLocked()
    {
        var label = UnlockBands.Label(UnlockBand.NotKnown);
        Assert.Equal("Not known", label);
        Assert.False(label.Contains("Locked", StringComparison.OrdinalIgnoreCase));

        var explanation = UnlockBands.Explanation(UnlockBand.NotKnown);
        Assert.Contains("never reported as available", explanation, StringComparison.Ordinal);
        Assert.All(UnlockBands.All, b => Assert.NotEmpty(UnlockBands.Explanation(b)));
    }

    /// <summary>Nothing ungradeable can reach the Available band. The three states that mean
    /// "Wayfarer does not know" all land in Not known, and so does any status added later — the
    /// mapping's fallback is deliberately the honest band rather than the optimistic one.</summary>
    [Theory]
    [InlineData(UnlockStatus.RequirementsUnknown)]
    [InlineData(UnlockStatus.UnknownGate)]
    [InlineData(UnlockStatus.Unverified)]
    public void NothingUngradeableIsBandedAsAvailable(UnlockStatus status)
    {
        Assert.Equal(UnlockBand.NotKnown, UnlockBands.Of(status));
    }

    [Theory]
    [InlineData(UnlockStatus.Available, UnlockBand.Available)]
    [InlineData(UnlockStatus.Accepted, UnlockBand.Available)]
    [InlineData(UnlockStatus.Done, UnlockBand.Complete)]
    [InlineData(UnlockStatus.LevelLocked, UnlockBand.Blocked)]
    [InlineData(UnlockStatus.QuestLocked, UnlockBand.Blocked)]
    [InlineData(UnlockStatus.InstanceLocked, UnlockBand.Blocked)]
    [InlineData(UnlockStatus.GrandCompanyLocked, UnlockBand.Blocked)]
    [InlineData(UnlockStatus.BeastTribeLocked, UnlockBand.Blocked)]
    [InlineData(UnlockStatus.MountLocked, UnlockBand.Blocked)]
    [InlineData(UnlockStatus.CollectionLocked, UnlockBand.Blocked)]
    [InlineData(UnlockStatus.LockedOut, UnlockBand.Blocked)]
    public void EveryStatusIsBanded(UnlockStatus status, UnlockBand expected)
    {
        Assert.Equal(expected, UnlockBands.Of(status));
    }

    /// <summary>Every value the enum has is covered by the theory above, so a status added later
    /// makes this fail and somebody has to decide which band it is in. Without it the new status
    /// would silently take the <c>_ =&gt;</c> arm and be filed as Not known, which is safe but is
    /// nobody's decision.</summary>
    [Fact]
    public void EveryStatusTheEnumHasIsAccountedFor()
    {
        var banded = new[]
        {
            UnlockStatus.Available, UnlockStatus.Accepted, UnlockStatus.Done, UnlockStatus.LevelLocked,
            UnlockStatus.QuestLocked, UnlockStatus.InstanceLocked, UnlockStatus.GrandCompanyLocked,
            UnlockStatus.BeastTribeLocked, UnlockStatus.MountLocked, UnlockStatus.CollectionLocked,
            UnlockStatus.LockedOut, UnlockStatus.RequirementsUnknown, UnlockStatus.UnknownGate,
            UnlockStatus.Unverified,
        };

        var all = Enum.GetValues<UnlockStatus>();
        var uncovered = all.Except(banded).ToList();
        Assert.True(
            uncovered.Count == 0,
            $"UnlockStatus has {uncovered.Count} value(s) no band test names: {string.Join(", ", uncovered)}.");
    }

    /// <summary>Bands appear in order inside a group, empty ones are left out, and the rows inside
    /// one are sorted by level and then name.</summary>
    [Fact]
    public void ABandedGroupIsOrderedAndCarriesNoEmptyBands()
    {
        var bands = UnlockSections.Band(
        [
            Entry("Zeta", UnlockStatus.Available, level: 50),
            Entry("Alpha", UnlockStatus.RequirementsUnknown, level: 10),
            Entry("Beta", UnlockStatus.Available, level: 50),
            Entry("Gamma", UnlockStatus.LevelLocked, level: 30),
            Entry("Delta", UnlockStatus.Available, level: 20),
        ]);

        Assert.Equal(AvailableBlockedNotKnown, bands.Select(b => b.Band));

        // Complete is not drawn when nothing is complete.
        Assert.DoesNotContain(UnlockBand.Complete, bands.Select(b => b.Band));

        // Level first, then name: Delta at 20, then Beta and Zeta both at 50 in name order.
        Assert.Equal(DeltaBetaZeta, bands[0].Entries.Select(e => e.Def.Unlock), StringComparer.Ordinal);
    }

    /// <summary>An entry no source states a level for sorts after the levelled ones rather than at
    /// level zero. The trophy mounts are that case, and sorting them first would put the hardest
    /// content in the catalogue at the top of a beginner's list.</summary>
    [Fact]
    public void AnEntryWithNoStatedLevelSortsAfterTheLevelledOnes()
    {
        var bands = UnlockSections.Band(
        [
            Entry("No level", UnlockStatus.Available, level: 0),
            Entry("Level 90", UnlockStatus.Available, level: 90),
        ]);

        Assert.Equal(LevelledThenNot, bands[0].Entries.Select(e => e.Def.Unlock), StringComparer.Ordinal);
    }

    /// <summary>Grouping by domain puts the groups in <see cref="UnlockDomains"/> order and bands
    /// each one — the shape the checklist actually draws.</summary>
    [Fact]
    public void GroupingByDomainOrdersTheGroupsAndBandsEachOne()
    {
        var sections = UnlockSections.Build(
            [
                Entry("A title", UnlockStatus.Available, level: 10, channel: "title"),
                Entry("A duty", UnlockStatus.LevelLocked, level: 20, channel: "duty"),
                Entry("A feature", UnlockStatus.Available, level: 15, channel: "system"),
                Entry("Another duty", UnlockStatus.Available, level: 5, channel: "duty"),
            ],
            UnlockGrouping.Domain);

        Assert.Equal(DutiesCapabilitiesTitles, sections.Select(s => s.Heading), StringComparer.Ordinal);

        var duties = sections[0];
        Assert.Equal(2, duties.Count);
        Assert.Equal(AvailableThenBlocked, duties.Bands.Select(b => b.Band));
    }

    /// <summary>The heading's count is the number of rows drawn under it, across every band. It used
    /// to be a separate <c>group.Count()</c> against a separately-ordered row list, which is a number
    /// and a list that can disagree.</summary>
    [Fact]
    public void AGroupHeadingsCountIsTheRowsBeneathIt()
    {
        var sections = UnlockSections.Build(
            [
                Entry("A", UnlockStatus.Available, level: 1, channel: "duty"),
                Entry("B", UnlockStatus.LevelLocked, level: 2, channel: "duty"),
                Entry("C", UnlockStatus.RequirementsUnknown, level: 3, channel: "duty"),
            ],
            UnlockGrouping.Domain);

        var group = Assert.Single(sections);
        Assert.Equal(3, group.Count);
        Assert.Equal(3, group.Bands.Sum(b => b.Entries.Count));
    }

    /// <summary>An entry whose quest is in no known zone gets a heading that reads as a statement
    /// rather than as the name of a zone.</summary>
    [Fact]
    public void AnEntryWithNoZoneIsHeadedHonestly()
    {
        var sections = UnlockSections.Build(
            [Entry("A", UnlockStatus.Available, level: 1)],
            UnlockGrouping.Zone);

        Assert.Equal(UnlockSections.NoZoneHeading, Assert.Single(sections).Heading);
    }

    private static ResolvedUnlock Entry(
        string name, UnlockStatus status, int level, string channel = "system") => new()
        {
            Def = new UnlockDefinition { Unlock = name, Channel = channel, Type = channel },
            QuestLevel = level,
            Status = status,
        };
}
