using Wayfarer.Modules.Quests;
using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>When a step draws bare ground to search as well as places that name something, which
/// of them the step is really about. The shapes are the ones the quest sheet really has: three
/// parts of a city and one of its people, six search areas and one bystander, and the ordinary
/// step with one place to go and one person to see.</summary>
public class QuestBystanderTests
{
    private static readonly Place Ground = new(1, 1, 10f, 0f, 10f, 35f);
    private static readonly Place MoreGround = new(1, 1, 90f, 0f, 90f, 35f);
    private static readonly Place ThirdGround = new(1, 1, 50f, 0f, 200f, 35f);
    private static readonly Place Someone = new(1, 1, 5f, 0f, 5f);

    [Fact]
    public void Bare_ground_outnumbering_the_named_is_what_the_step_means()
    {
        // "Speak with the people of Old Sharlayan": three parts of the city, one of the people.
        var places = QuestReader.Standing([Ground, MoreGround, ThirdGround], [Someone]);

        Assert.Equal([Ground, MoreGround, ThirdGround], places);
    }

    [Fact]
    public void One_place_to_go_and_one_person_to_see_keeps_the_person()
    {
        // "Speak with Hien": the person is the better answer, not the ground they stand on.
        Assert.Empty(QuestReader.Standing([Ground], [Someone]));
    }

    [Fact]
    public void Ground_matching_the_named_one_for_one_keeps_both()
    {
        Assert.Empty(QuestReader.Standing([Ground, MoreGround], [Someone, Someone]));
    }

    [Fact]
    public void A_step_that_names_something_everywhere_is_left_alone()
    {
        Assert.Empty(QuestReader.Standing([], [Someone, Someone]));
    }

    [Fact]
    public void A_step_that_is_all_bare_ground_is_left_alone()
    {
        // Nothing to take out, so nothing is said: the caller keeps what it had.
        Assert.Empty(QuestReader.Standing([Ground, MoreGround], []));
    }
}
