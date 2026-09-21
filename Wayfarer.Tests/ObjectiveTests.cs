using Wayfarer.Guidance;
using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>Which entry of an objective the app routes to.</summary>
public class ObjectiveTests
{
    private static readonly Place There = new(1, 1, 100f, 0f, 0f);
    private static readonly Place Standing = new(1, 1, 0f, 0f, 0f);

    [Fact]
    public void The_nearest_entry_is_the_target_whatever_order_the_sheet_wrote_them_in()
    {
        // One step of a quest, three lines: speak with each of three leaders, in any order. The
        // sheet writes them in its own order and the player is standing beside the second.
        var far = new ObjectiveEntry("Speak with Kan-E-Senna.", null, new Destination.Reachable([new Place(1, 1, 900f, 0f, 0f)]));
        var near = new ObjectiveEntry("Speak with Merlwyb.", null, new Destination.Reachable([There]));

        var objective = new Objective("The Legacy of Our Fathers", [far, near]);

        Assert.Same(near, objective.Guided(Standing));
    }

    [Fact]
    public void A_thing_picked_out_of_the_world_is_measured_where_it_stands()
    {
        var far = new ObjectiveEntry("Speak with one.", null, new Destination.AtObject(1uL, new Place(1, 1, 900f, 0f, 0f)));
        var near = new ObjectiveEntry("Speak with another.", null, new Destination.AtObject(2uL, There));

        Assert.Same(near, new Objective("Three Collectors", [far, near]).Guided(Standing));
    }

    [Fact]
    public void An_entry_in_this_zone_beats_a_nearer_one_in_another()
    {
        // Two zones hold the same numbers, so a place a world away can read as underfoot.
        var elsewhere = new ObjectiveEntry("Elsewhere.", null, new Destination.Reachable([new Place(2, 2, 1f, 0f, 0f)]));
        var here = new ObjectiveEntry("Here.", null, new Destination.Reachable([There]));

        Assert.Same(here, new Objective("Across the sea", [elsewhere, here]).Guided(Standing));
    }

    [Fact]
    public void Without_knowing_where_the_player_stands_the_first_entry_holds()
    {
        var first = new ObjectiveEntry("Attune.", null, new Destination.Reachable([new Place(1, 1, 900f, 0f, 0f)]));
        var second = new ObjectiveEntry("Visit the guild.", null, new Destination.Reachable([There]));

        Assert.Same(first, new Objective("Close to Home", [first, second]).Guided());
    }

    [Fact]
    public void An_entry_with_nowhere_to_go_is_skipped_for_the_next_that_has()
    {
        var wait = new ObjectiveEntry("Wait for nightfall.", null, new Destination.Blocked("no map location for this step"));
        var guild = new ObjectiveEntry("Visit the guild.", null, new Destination.Reachable([There]));

        Assert.Same(guild, new Objective("A Vigil", [wait, guild]).Guided(Standing));
    }

    [Fact]
    public void A_duty_is_somewhere_to_go()
    {
        var duty = new ObjectiveEntry("Complete the duty.", null, new Destination.InDuty(7));

        Assert.Same(duty, new Objective("Trial", [duty]).Guided(Standing));
    }

    [Fact]
    public void Somewhere_to_walk_is_preferred_to_a_duty_to_queue_for()
    {
        // A duty is not a point on a map, so it cannot be compared by distance. It is still
        // somewhere to be sent — just not while the step also names ground the player can reach.
        var duty = new ObjectiveEntry("Complete the duty.", null, new Destination.InDuty(7));
        var walk = new ObjectiveEntry("Speak with the marshal.", null, new Destination.Reachable([new Place(1, 1, 900f, 0f, 0f)]));

        Assert.Same(walk, new Objective("Storming the Hull", [duty, walk]).Guided(Standing));
    }

    [Fact]
    public void Equal_distances_keep_the_order_the_module_gave_them()
    {
        var first = new ObjectiveEntry("One.", null, new Destination.Reachable([There]));
        var second = new ObjectiveEntry("Two.", null, new Destination.Reachable([There]));

        Assert.Same(first, new Objective("A tie", [first, second]).Guided(Standing));
    }

    [Fact]
    public void A_step_that_is_nowhere_is_not_somewhere_to_go()
    {
        var blocked = new ObjectiveEntry("Wait.", null, new Destination.Blocked("no map location for this step"));

        Assert.Null(new Objective("Waiting", [blocked]).Guided(Standing));
    }
}
