using Wayfarer.Guidance;
using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>Which entry of an objective the app routes to.</summary>
public class ObjectiveTests
{
    private static readonly Place There = new(1, 1, 100f, 0f, 0f);

    [Fact]
    public void The_first_entry_in_list_order_is_the_target_even_when_a_later_one_is_nearer()
    {
        var aetheryte = new ObjectiveEntry("Attune.", null, new Destination.Reachable([new Place(1, 1, 900f, 0f, 0f)]));
        var guild = new ObjectiveEntry("Visit the guild.", null, new Destination.Reachable([There]));

        var objective = new Objective("Close to Home", [aetheryte, guild]);

        Assert.Same(aetheryte, objective.Guided());
    }

    [Fact]
    public void An_entry_with_nowhere_to_go_is_skipped_for_the_next_that_has()
    {
        var wait = new ObjectiveEntry("Wait for nightfall.", null, new Destination.Blocked("no map location for this step"));
        var guild = new ObjectiveEntry("Visit the guild.", null, new Destination.Reachable([There]));

        var objective = new Objective("A Vigil", [wait, guild]);

        Assert.Same(guild, objective.Guided());
    }

    [Fact]
    public void A_duty_is_somewhere_to_go()
    {
        var duty = new ObjectiveEntry("Complete the duty.", null, new Destination.InDuty(7));

        Assert.Same(duty, new Objective("Trial", [duty]).Guided());
    }

    [Fact]
    public void A_step_that_is_nowhere_is_not_somewhere_to_go()
    {
        var blocked = new ObjectiveEntry("Wait.", null, new Destination.Blocked("no map location for this step"));

        Assert.Null(new Objective("Waiting", [blocked]).Guided());
    }
}
