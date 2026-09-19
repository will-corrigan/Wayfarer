using Wayfarer.Guidance;
using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>When the app publishes. The rule under test: words and the shape of the route
/// publish; a walk getting shorter as the player moves does not.</summary>
public class GuidanceChangeTests
{
    private static readonly FakeSource Quests = new("Quests");
    private static readonly Place Here = new(1, 1, 0f, 0f, 0f);
    private static readonly Place There = new(1, 1, 100f, 0f, 0f);

    [Fact]
    public void Nothing_to_nothing_is_no_change()
    {
        Assert.True(GuidanceChange.IsSame(null, null));
    }

    [Fact]
    public void Nothing_to_something_is_a_change()
    {
        Assert.False(GuidanceChange.IsSame(null, Guide(Walk(250f))));
    }

    [Fact]
    public void A_walk_getting_shorter_is_not_a_change()
    {
        Assert.True(GuidanceChange.IsSame(Guide(Walk(250f)), Guide(Walk(180f))));
    }

    [Fact]
    public void A_different_leg_shape_is_a_change()
    {
        var walk = Guide(Walk(250f));
        var teleport = Guide(new Route([new Leg.Teleport(10, "Field"), new Leg.Walk(There, 40f)], 160f, There));

        Assert.False(GuidanceChange.IsSame(walk, teleport));
    }

    [Fact]
    public void A_different_aetheryte_is_a_change_even_at_the_same_shape()
    {
        var a = Guide(new Route([new Leg.Teleport(10, "Field"), new Leg.Walk(There, 40f)], 160f, There));
        var b = Guide(new Route([new Leg.Teleport(11, "Forest"), new Leg.Walk(There, 40f)], 160f, There));

        Assert.False(GuidanceChange.IsSame(a, b));
    }

    [Fact]
    public void The_route_switching_to_another_place_is_a_change()
    {
        var elsewhere = There with { X = 500f };

        Assert.False(GuidanceChange.IsSame(Guide(Walk(100f)), Guide(new Route([new Leg.Walk(elsewhere, 500f)], 500f, elsewhere))));
    }

    [Fact]
    public void Different_words_are_a_change()
    {
        var a = Guide(Walk(100f));
        var b = a with { Objective = a.Objective with { Headline = "Another Quest" } };

        Assert.False(GuidanceChange.IsSame(a, b));
    }

    [Fact]
    public void A_count_ticking_up_is_a_change()
    {
        var a = Guide(Walk(100f));
        var entry = a.Objective.Entries[0] with { Progress = new Progress(2, 3) };
        var b = a with { Objective = a.Objective with { Entries = [entry] } };

        Assert.False(GuidanceChange.IsSame(a, b));
    }

    [Fact]
    public void Another_source_saying_the_same_words_is_a_change()
    {
        var a = Guide(Walk(100f));
        var b = a with { Source = new FakeSource("Hunting") };

        Assert.False(GuidanceChange.IsSame(a, b));
    }

    [Fact]
    public void Equal_content_built_twice_is_no_change()
    {
        Assert.True(GuidanceChange.IsSame(Guide(Walk(100f)), Guide(Walk(100f))));
    }

    [Fact]
    public void A_different_action_on_the_same_words_is_a_change()
    {
        var a = Guide(Walk(100f));
        var b = a with { Target = a.Target! with { Action = new EntryAction.Say("Well met!") } };

        Assert.False(GuidanceChange.IsSame(a, b));
    }

    [Fact]
    public void The_route_moving_to_the_next_entry_is_a_change()
    {
        var momodi = new ObjectiveEntry("Speak with Momodi.", null, new Destination.Reachable([There]));
        var aetheryte = new ObjectiveEntry("Attune to the aetheryte.", null, new Destination.Reachable([There]));
        var objective = new Objective("Close to Home", null, [momodi, aetheryte]);

        var a = new PublishedGuidance(Quests, objective, momodi, Walk(100f));
        var b = new PublishedGuidance(Quests, objective, aetheryte, Walk(100f));

        Assert.False(GuidanceChange.IsSame(a, b));
    }

    [Fact]
    public void A_different_quest_behind_the_same_headline_is_a_change()
    {
        var a = Guide(Walk(100f));
        var b = a with { Objective = a.Objective with { HeadlinePressable = true } };

        Assert.False(GuidanceChange.IsSame(a, b));
    }

    private static Route Walk(float yalms) => new([new Leg.Walk(There, yalms)], yalms, There);

    private static PublishedGuidance Guide(Route route)
    {
        var entry = new ObjectiveEntry("Speak with Momodi.", new Progress(1, 3), new Destination.Reachable([There]));
        return new PublishedGuidance(Quests, new Objective("The Ul'dahn Envoy", null, [entry]), entry, route);
    }
}
