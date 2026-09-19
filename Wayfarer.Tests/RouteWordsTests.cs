using Wayfarer.Core.Guidance;
using Wayfarer.Core.Presentation;
using Wayfarer.Core.Routing;

namespace Wayfarer.Tests;

/// <summary>What the route line says, marks and does for each shape of guidance.</summary>
public class RouteWordsTests
{
    private static readonly Place End = new(1, 1, 0f, 0f, 0f);
    private static readonly FakeSource Quests = new();

    [Fact]
    public void A_walk_alone_has_no_line()
    {
        Assert.Null(RouteWords.Compose(Guide(new Route([new Leg.Walk(End, 80f)], 80f, End))));
    }

    [Fact]
    public void A_teleport_first_names_the_aetheryte_wears_its_mark_and_teleports_on_press()
    {
        var line = RouteWords.Compose(Guide(new Route([new Leg.Teleport(2, "Bentbranch Meadows"), new Leg.Walk(End, 80f)], 200f, End)));

        Assert.Equal(new RouteLine(RouteGlyph.Aetheryte, "Teleport to Bentbranch Meadows", new RoutePress.Teleport(2)), line);
    }

    [Fact]
    public void Every_leg_the_player_acts_on_is_named_in_order()
    {
        var route = new Route(
            [
                new Leg.Teleport(8, "Limsa Lominsa Lower Decks"),
                new Leg.Walk(End, 30f),
                new Leg.ShardHop("The Aftcastle", "The Bismarck"),
                new Leg.Walk(End, 10f),
                new Leg.Door("Drowning Wench"),
                new Leg.Walk(End, 5f),
            ],
            230f,
            End);

        Assert.Equal(
            "Teleport to Limsa Lominsa Lower Decks, then Aethernet to The Bismarck, then Through Drowning Wench",
            RouteWords.Describe(route));
    }

    [Fact]
    public void A_door_first_is_words_only()
    {
        var line = RouteWords.Compose(Guide(new Route([new Leg.Door("Drowning Wench"), new Leg.Walk(End, 5f)], 5f, End)));

        Assert.Equal(new RouteLine(RouteGlyph.None, "Through Drowning Wench", null), line);
    }

    [Fact]
    public void No_route_to_a_known_target_says_so()
    {
        Assert.Equal(new RouteLine(RouteGlyph.None, RouteWords.NoRoute, null), RouteWords.Compose(Guide(null)));
    }

    [Fact]
    public void A_duty_target_opens_the_duty_finder()
    {
        var entry = new ObjectiveEntry("Clear Sastasha.", null, new Destination.InDuty(4));
        var guidance = new PublishedGuidance(Quests, new Objective("It's Probably Pirates", null, [entry]), entry, null);

        Assert.Equal(new RouteLine(RouteGlyph.Duty, RouteWords.DutyWords, new RoutePress.OpenDuty(4)), RouteWords.Compose(guidance));
    }

    [Fact]
    public void Nothing_guided_is_no_line()
    {
        Assert.Null(RouteWords.Compose(null));
    }

    private static PublishedGuidance Guide(Route? route)
    {
        var entry = new ObjectiveEntry("Speak with Momodi.", null, new Destination.Reachable([End]));
        return new PublishedGuidance(Quests, new Objective("The Ul'dahn Envoy", null, [entry]), entry, route);
    }
}
