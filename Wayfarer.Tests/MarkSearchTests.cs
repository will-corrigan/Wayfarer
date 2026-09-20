using Wayfarer.Guidance;
using Wayfarer.Routing;
using Wayfarer.World;

namespace Wayfarer.Tests;

/// <summary>Which of the things standing in a circle a step is about.
///
/// <para>Both fixtures are real, written down from the world as it stood during "The Road Home",
/// whose step asks the player to find three wounded soldiers. The second is the one that matters:
/// two soldiers of the same name, both within the circle, both of them things the player could act
/// on, one already helped and one not. Nothing about them differs but the mark the game draws over
/// the head of the one still asking.</para></summary>
public class MarkSearchTests
{
    private const uint Quest = 68074;

    /// <summary>The mark the game drew over the soldier who still wanted helping.</summary>
    private const uint Asking = 71203;

    private static readonly Place Circle = new(620, 368, -183.0f, 281.2f, 288.8f, 80f);
    private static readonly Place Standing = new(620, 368, -148.4f, 300.6f, 261.9f);

    // Read off the world at 23:16: the distance each stood from the middle of the circle.
    private static readonly Candidate Helped = Out(1023834, plate: 0, targetable: true, 41.5f);
    private static readonly Candidate StillAsking = Out(1020623, Asking, targetable: true, 30.8f);
    private static readonly Candidate Alisaie = Out(1020409, 0, targetable: false, 16.1f);
    private static readonly Candidate Alphinaud = Out(1020408, 0, targetable: false, 18.5f);
    private static readonly Candidate Adventurer = Out(0, 0, targetable: true, 43.8f);
    private static readonly Candidate TheirCat = Out(110, 0, targetable: true, 45.4f);
    private static readonly Candidate Kongamato = Out(6652, 0, targetable: true, 12.6f);
    private static readonly Candidate Destination = Out(2009008, 0, targetable: false, 18.0f);

    private static readonly Candidate[] TheCircle =
        [Adventurer, TheirCat, Kongamato, Alisaie, Helped, Alphinaud, StillAsking, Destination];

    /// <summary>What the quest names: everyone the errand involves, under ACTOR.</summary>
    private static readonly Mark[] ItsPeople =
    [
        new(1020611, MarkKind.Person), new(1020613, MarkKind.Person), new(1020623, MarkKind.Person),
        new(1023834, MarkKind.Person), new(1023835, MarkKind.Person), new(1023836, MarkKind.Person),
        new(1023837, MarkKind.Person), new(1020409, MarkKind.Person), new(1020408, MarkKind.Person),
    ];

    [Fact]
    public void The_one_the_game_still_marks_is_the_one_to_go_to()
    {
        // The one already helped stands nearer. Going by distance alone sends the player back to
        // someone who wants nothing, over and over, which is what happens when the plugin is
        // loaded again part way through and forgets what it had already done.
        var nearest = MarkSearch.Choose(Circle, TheCircle, ItsPeople, Quest, Standing);

        Assert.Equal(StillAsking.At, nearest?.At);
    }

    [Fact]
    public void Nearest_still_decides_between_two_that_are_both_asking()
    {
        var far = Near(1023835, Asking, targetable: true, 35f);
        var near = Near(1023836, Asking, targetable: true, 5f);

        var nearest = MarkSearch.Choose(Circle, [far, near], ItsPeople, Quest, Standing);

        Assert.Equal(near.At, nearest?.At);
    }

    [Fact]
    public void When_the_game_marks_none_of_them_the_nearest_answers()
    {
        var nearest = MarkSearch.Choose(Circle, [Helped], ItsPeople, Quest, Standing);

        Assert.Equal(Helped.At, nearest?.At);
    }

    [Fact]
    public void Things_that_carry_no_mark_are_chosen_between_as_they_always_were()
    {
        // A dug hole, a sparkling thing, a lever: the game draws nothing over any of them, so
        // there is nothing to prefer by and the nearest is still the answer.
        var far = Near(2008944, 0, targetable: true, 35f);
        var near = Near(2008945, 0, targetable: true, 5f);
        Mark[] things = [new(2008944, MarkKind.Thing), new(2008945, MarkKind.Thing)];

        Assert.Equal(near.At, MarkSearch.Choose(Circle, [far, near], things, Quest, Standing)?.At);
    }

    [Fact]
    public void A_marked_thing_is_preferred_only_among_things_and_never_over_a_whole_sort()
    {
        // One person is marked and no object is. The object must still win, because a marked
        // anybody does not outrank the one thing a step actually names.
        var thing = Near(2008944, 0, targetable: true, 35f);
        var person = Near(1020623, Asking, targetable: true, 5f);
        Mark[] both = [new(2008944, MarkKind.Thing), new(1020623, MarkKind.Person)];

        Assert.Equal(thing.At, MarkSearch.Choose(Circle, [person, thing], both, Quest, Standing)?.At);
    }

    [Fact]
    public void One_the_player_cannot_act_on_is_passed_over_however_it_is_marked()
    {
        var inert = Near(1023835, Asking, targetable: false, 2f);

        var nearest = MarkSearch.Choose(Circle, [inert, StillAsking], ItsPeople, Quest, Standing);

        Assert.Equal(StillAsking.At, nearest?.At);
    }

    [Fact]
    public void Naming_nobody_is_how_the_circle_stayed_the_only_answer()
    {
        // What the guidance did before a quest's people were read at all.
        var nearest = MarkSearch.Choose(Circle, TheCircle, [], Quest, Standing);

        Assert.Null(nearest);
    }

    [Fact]
    public void A_thing_the_quest_names_beats_anyone_standing_nearer()
    {
        // ACTOR is the whole cast of an errand; EOBJECT is the one thing a step is about. A
        // passing face must never take the answer from a named thing, however much nearer.
        var thing = Near(2008944, 0, targetable: true, 35f);
        var person = Near(1020623, Asking, targetable: true, 5f);
        Mark[] both = [new(2008944, MarkKind.Thing), new(1020623, MarkKind.Person)];

        Assert.Equal(thing.At, MarkSearch.Choose(Circle, [person, thing], both, Quest, Standing)?.At);
        Assert.Equal(person.At, MarkSearch.Choose(Circle, [person], both, Quest, Standing)?.At);
    }

    [Fact]
    public void A_creature_answers_only_when_nothing_and_nobody_does()
    {
        var beast = Near(5000, 0, targetable: true, 20f);
        var person = Near(1020623, Asking, targetable: true, 40f);
        Mark[] both = [new(1020623, MarkKind.Person), new(5000, MarkKind.Creature)];

        Assert.Equal(person.At, MarkSearch.Choose(Circle, [beast, person], both, Quest, Standing)?.At);
        Assert.Equal(beast.At, MarkSearch.Choose(Circle, [beast], both, Quest, Standing)?.At);
    }

    [Fact]
    public void What_the_game_spawned_for_the_event_is_found_without_being_named()
    {
        var spawned = new Candidate(1, 9999, Quest, true, 0, Near(9999, 0, true, 10f).At);

        Assert.Equal(spawned.At, MarkSearch.Choose(Circle, [spawned], [], Quest, Standing)?.At);
    }

    [Fact]
    public void Someone_standing_outside_the_circle_is_not_in_it()
    {
        var far = Out(1020623, Asking, targetable: true, 200f);

        var nearest = MarkSearch.Choose(Circle, [far], ItsPeople, Quest, Standing);

        Assert.Null(nearest);
    }

    [Fact]
    public void A_place_with_no_room_in_it_is_not_searched()
    {
        Place point = new(620, 368, -183.0f, 281.2f, 288.8f);

        Assert.Null(MarkSearch.Choose(point, TheCircle, ItsPeople, Quest, Standing));
    }

    [Fact]
    public void Nobody_is_taken_for_someone_the_quest_named_by_accident()
    {
        // An adventurer, their cat and a wild beast, all standing in it, none of them the step.
        var nearest = MarkSearch.Choose(Circle, [Adventurer, TheirCat, Kongamato], ItsPeople, Quest, Standing);

        Assert.Null(nearest);
    }

    /// <summary>A unique id for one thing standing somewhere, so two of a kind are still two.</summary>
    private static ulong Instance(uint baseId, float where) => ((ulong)baseId << 16) + (ulong)(where * 10f);

    /// <summary>One thing standing the given distance from the player, on the way in towards the
    /// middle of the circle, for when how far there is to walk is the point.</summary>
    private static Candidate Near(uint baseId, uint plate, bool targetable, float fromPlayer)
    {
        var (dx, dz) = (Circle.X - Standing.X, Circle.Z - Standing.Z);
        var length = MathF.Sqrt((dx * dx) + (dz * dz));
        return new Candidate(
            Instance(baseId, fromPlayer),
            baseId,
            0,
            targetable,
            plate,
            new Place(620, 368, Standing.X + (dx / length * fromPlayer), 300f, Standing.Z + (dz / length * fromPlayer)));
    }

    /// <summary>One thing standing the given distance out from the middle of the circle, along the
    /// line towards where the player is, so that further from the middle is also further to walk.
    /// </summary>
    private static Candidate Out(uint baseId, uint plate, bool targetable, float fromMiddle)
    {
        var (dx, dz) = (Standing.X - Circle.X, Standing.Z - Circle.Z);
        var length = MathF.Sqrt((dx * dx) + (dz * dz));
        return new Candidate(
            Instance(baseId, fromMiddle),
            baseId,
            0,
            targetable,
            plate,
            new Place(620, 368, Circle.X + (dx / length * fromMiddle), 300f, Circle.Z + (dz / length * fromMiddle)));
    }
}
