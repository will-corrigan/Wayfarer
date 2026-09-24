using Wayfarer.Guidance;
using Wayfarer.Modules.Hunting;
using Wayfarer.Routing;
using Wayfarer.World;

namespace Wayfarer.Tests;

/// <summary>What a followed hunting log page or mark bill guides to: its targets in the order the
/// game lists them, one at a time, until every one is done.</summary>
public class HuntObjectiveBuilderTests
{
    private static readonly Place Peacegarden = new(154, 7, 10f, 0f, 10f);
    private static readonly Place Footfalls = new(140, 20, -300f, 0f, -700f);
    private static readonly Place FateGround = new(140, 20, -353.7f, 15f, -732.1f, 40f);

    private static readonly Quarry WaterSprite = new("Water Sprite", 59, 3, 3, [Peacegarden]);
    private static readonly Quarry MidgeSwarm = new("Midge Swarm", 60, 0, 3, [Peacegarden]);
    private static readonly Quarry Microchu = new("Microchu", 61, 1, 3, [Peacegarden]);

    [Fact]
    public void The_first_target_not_yet_done_is_the_one_guided_to()
    {
        var objective = Build([WaterSprite, MidgeSwarm, Microchu]);

        var entry = Assert.Single(objective!.Entries);
        Assert.Equal("Midge Swarm", entry.Text);
        Assert.Equal(new Progress(0, 3), entry.Progress);
        Assert.Equal([Peacegarden], Assert.IsType<Destination.Reachable>(entry.Where).Places);
    }

    [Fact]
    public void Order_is_kept_even_when_a_later_target_is_further_along()
    {
        // Microchu has kills already, Midge Swarm has none: the page is still done in order.
        var objective = Build([MidgeSwarm, Microchu]);

        Assert.Equal("Midge Swarm", Assert.Single(objective!.Entries).Text);
    }

    [Fact]
    public void Nothing_left_to_do_is_no_objective_at_all()
    {
        Assert.Null(Build([WaterSprite, WaterSprite with { Name = "Other" }]));
    }

    [Fact]
    public void A_target_standing_in_sight_is_gone_to_directly()
    {
        var seen = new Found(42, new Place(154, 7, 12f, 0f, 14f));

        var objective = Build([MidgeSwarm], seen: nameId => nameId == 60 ? seen : null);

        var at = Assert.IsType<Destination.AtObject>(Assert.Single(objective!.Entries).Where);
        Assert.Equal(42uL, at.Id);
    }

    [Fact]
    public void A_target_that_lives_inside_a_duty_is_queued_for()
    {
        var inside = new Quarry("Sapsa Shelfspine", 70, 0, 3, [], Duty: 4);

        var objective = Build([inside]);

        Assert.Equal(4u, Assert.IsType<Destination.InDuty>(Assert.Single(objective!.Entries).Where).DutyId);
    }

    [Fact]
    public void A_fate_target_says_so_and_goes_to_where_the_fate_usually_is()
    {
        var longlegs = new Quarry("Daddy Longlegs", 80, 0, 1, [Footfalls], Fate: new QuarryFate(366, "He's Got Legs"));

        var entry = Assert.Single(Build([longlegs])!.Entries);

        Assert.Equal("Daddy Longlegs (FATE: He's Got Legs, not up right now)", entry.Text);
        Assert.Equal([Footfalls], Assert.IsType<Destination.Reachable>(entry.Where).Places);
    }

    [Fact]
    public void A_fate_target_in_another_zone_is_not_said_to_be_down()
    {
        // The game only knows the FATEs of the zone the player is standing in.
        var longlegs = new Quarry("Daddy Longlegs", 80, 0, 1, [Footfalls], Fate: new QuarryFate(366, "He's Got Legs"));

        var entry = Assert.Single(Build([longlegs], fateKnown: _ => false)!.Entries);

        Assert.Equal("Daddy Longlegs (FATE: He's Got Legs)", entry.Text);
    }

    [Fact]
    public void A_fate_target_whose_fate_is_up_goes_to_the_fate()
    {
        var longlegs = new Quarry("Daddy Longlegs", 80, 0, 1, [Footfalls], Fate: new QuarryFate(366, "He's Got Legs"));

        var entry = Assert.Single(Build([longlegs], fateAt: id => id == 366 ? FateGround : null)!.Entries);

        Assert.Equal("Daddy Longlegs (FATE: He's Got Legs)", entry.Text);
        Assert.Equal([FateGround], Assert.IsType<Destination.Reachable>(entry.Where).Places);
    }

    [Fact]
    public void The_headline_and_kind_are_the_hunts_own()
    {
        var objective = Build([MidgeSwarm]);

        Assert.Equal("Conjurer, rank 1", objective!.Headline);
        Assert.Equal("Hunting Log", objective.Kind);
    }

    private static Objective? Build(
        IReadOnlyList<Quarry> quarries,
        Func<uint, Found?>? seen = null,
        Func<uint, Place?>? fateAt = null,
        Func<uint, bool>? fateKnown = null) =>
        HuntObjectiveBuilder.Build("Conjurer, rank 1", "Hunting Log", quarries, seen ?? (_ => null), fateAt ?? (_ => null), fateKnown ?? (_ => true));
}
