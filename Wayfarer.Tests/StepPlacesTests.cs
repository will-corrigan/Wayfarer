using Wayfarer.Modules.Quests;
using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>Which of the places a to-do line names the line is about. The shapes are the ones the
/// quest sheet really has: a door carried through a whole quest, a person a quest sends you back
/// to again and again, three parts of a city with one of its people among them, and the ordinary
/// line with one place to go.</summary>
public class StepPlacesTests
{
    private const uint Zone = 620;

    private static readonly Place Ground = new(Zone, 1, 10f, 0f, 10f, 35f);
    private static readonly Place MoreGround = new(Zone, 1, 90f, 0f, 90f, 35f);
    private static readonly Place ThirdGround = new(Zone, 1, 50f, 0f, 200f, 35f);
    private static readonly Place ByTheDoor = new(Zone, 1, 5f, 0f, 5f);
    private static readonly Place Elsewhere = new(Zone, 1, 400f, 0f, 400f);

    [Fact]
    public void A_door_carried_through_a_quest_is_not_where_any_step_sends_you()
    {
        // "Heavens Weep": a sealed door hung on five steps of six, each step's own place once.
        var steps = Quest(
            Step("Follow Alisaie.", Person(1, Elsewhere)),
            Step("Speak with Raubahn.", Person(2, Elsewhere), Door()),
            Step("Search for wounded soldiers.", Area(Ground), Area(Ground), Area(Ground), Door()),
            Step("Continue searching.", Person(3, Elsewhere), Door()),
            Step("Speak with Lyse.", Person(4, Elsewhere), Door()),
            Step("Speak with Alphinaud.", Person(5, Elsewhere), Door()));

        var chosen = StepPlaces.Choose(steps);

        Assert.Equal([Ground, Ground, Ground], chosen[2]);
        Assert.Equal([Elsewhere], chosen[1]);
        Assert.Equal([Elsewhere], chosen[5]);
    }

    [Fact]
    public void A_step_that_names_the_door_still_goes_to_it()
    {
        // "Pass through the portal": the same door, on a step that asks for it.
        var steps = Quest(
            Step("Pass through the portal of wisdom.", Area(Ground), Door("portal of wisdom")),
            Step("Search the area for denizens.", Area(Ground), Door("portal of wisdom")),
            Step("Speak with G'raha Tia.", Person(1, Elsewhere), Door("portal of wisdom")));

        var chosen = StepPlaces.Choose(steps);

        Assert.Contains(ByTheDoor, chosen[0]);
        Assert.DoesNotContain(ByTheDoor, chosen[1]);
        Assert.DoesNotContain(ByTheDoor, chosen[2]);
    }

    [Fact]
    public void A_person_a_quest_sends_you_back_to_is_never_treated_as_scenery()
    {
        var steps = Quest(
            Step("Speak with Jacke.", Person(9, ByTheDoor), Area(Ground)),
            Step("Report to Jacke.", Person(9, ByTheDoor), Area(MoreGround)),
            Step("Speak with Jacke again.", Person(9, ByTheDoor), Area(ThirdGround)));

        var chosen = StepPlaces.Choose(steps);

        Assert.All(chosen, places => Assert.Contains(ByTheDoor, places));
    }

    [Fact]
    public void Ground_outnumbering_the_named_is_what_the_step_means()
    {
        // "Speak with the people of Old Sharlayan": three parts of the city, one of its people.
        var steps = Quest(Step("Speak with the people of Old Sharlayan.", Area(Ground), Area(MoreGround), Area(ThirdGround), Person(1, Elsewhere)));

        Assert.Equal([Ground, MoreGround, ThirdGround], StepPlaces.Choose(steps)[0]);
    }

    [Fact]
    public void One_place_to_go_and_one_person_to_see_keeps_the_person()
    {
        // "Speak with Hien": one bare place beside one person is not a bystander.
        var steps = Quest(Step("Speak with Hien.", Area(Ground), Person(1, Elsewhere)));

        Assert.Equal([Ground, Elsewhere], StepPlaces.Choose(steps)[0]);
    }

    [Fact]
    public void A_step_with_one_place_keeps_it_whatever_the_quest_does_with_it()
    {
        var steps = Quest(
            Step("Wash the bedsheet.", Door("washtub")),
            Step("Wash it again.", Door("washtub")),
            Step("Report to Ponnixia.", Person(1, Elsewhere)));

        Assert.Equal([ByTheDoor], StepPlaces.Choose(steps)[0]);
    }

    [Fact]
    public void A_line_that_names_nowhere_says_nowhere()
    {
        Assert.Empty(StepPlaces.Choose(Quest(Step("Wait for nightfall.")))[0]);
    }

    private static StepShape[] Quest(params StepShape[] steps) => steps;

    private static StepShape Step(string words, params StepPlace[] places) => new(0, 1, words, places);

    private static StepPlace Area(Place at) => new(Row(at), 0, false, string.Empty, at);

    private static StepPlace Person(uint id, Place at) => new(2000 + id, 1_000_000 + id, false, string.Empty, at);

    private static StepPlace Door(string name = "cermet bulkhead") => new(99, 2_008_944, true, name, ByTheDoor);

    /// <summary>A Level row id standing for one place, so the same place is the same row.</summary>
    private static uint Row(Place at) => 1000 + (uint)(at.X + at.Z);
}
