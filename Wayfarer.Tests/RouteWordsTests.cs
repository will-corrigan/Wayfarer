using Wayfarer.Core.Presentation;
using Wayfarer.Core.Routing;

namespace Wayfarer.Tests;

/// <summary>What the route line says for each shape of route.</summary>
public class RouteWordsTests
{
    private static readonly Place End = new(1, 1, 0f, 0f, 0f);

    [Fact]
    public void A_walk_alone_has_no_words()
    {
        Assert.Null(RouteWords.Describe(new Route([new Leg.Walk(80f)], 80f, End)));
    }

    [Fact]
    public void A_teleport_then_a_walk_names_the_aetheryte_only()
    {
        var route = new Route([new Leg.Teleport(2, "Bentbranch Meadows"), new Leg.Walk(80f)], 200f, End);

        Assert.Equal("Teleport to Bentbranch Meadows", RouteWords.Describe(route));
    }

    [Fact]
    public void Every_leg_the_player_acts_on_is_named_in_order()
    {
        var route = new Route(
            [
                new Leg.Teleport(8, "Limsa Lominsa Lower Decks"),
                new Leg.Walk(30f),
                new Leg.ShardHop("The Aftcastle", "The Bismarck"),
                new Leg.Walk(10f),
                new Leg.Door("Drowning Wench"),
                new Leg.Walk(5f),
            ],
            230f,
            End);

        Assert.Equal(
            "Teleport to Limsa Lominsa Lower Decks, then Aethernet to The Bismarck, then Through Drowning Wench",
            RouteWords.Describe(route));
    }
}
