using Wayfarer.Modules.Quests;

namespace Wayfarer.Tests;

/// <summary>Which of a quest's places belong to the quest rather than to any one of its steps.
/// The fixtures are the shapes the quest sheet really has, measured across every quest in the
/// game: a door hung on five steps of six, a giver hung on all eight, an errand that comes round
/// twice out of seven.</summary>
public class QuestFurnitureTests
{
    [Fact]
    public void A_place_on_more_than_half_a_quests_steps_belongs_to_the_quest()
    {
        // "Heavens Weep": the sealed door on every step after the first, the step's own place once.
        IReadOnlyList<uint>[] steps = [[10], [20, 99], [30, 99], [40, 99], [50, 99], [60, 99]];

        Assert.Equal([99u], QuestReader.Furniture(steps));
    }

    [Fact]
    public void A_place_on_exactly_half_the_steps_is_left_alone()
    {
        IReadOnlyList<uint>[] steps = [[10, 99], [20, 99], [30], [40]];

        Assert.Empty(QuestReader.Furniture(steps));
    }

    [Fact]
    public void An_errand_that_comes_round_again_is_not_furniture()
    {
        // The same NPC spoken to twice in seven steps is still what those two steps are about.
        IReadOnlyList<uint>[] steps = [[10], [20], [30], [40], [50], [60], [20]];

        Assert.Empty(QuestReader.Furniture(steps));
    }

    [Fact]
    public void A_giver_hung_on_every_step_belongs_to_the_quest()
    {
        IReadOnlyList<uint>[] steps = [[10, 7], [20, 7], [30, 7]];

        Assert.Equal([7u], QuestReader.Furniture(steps));
    }

    [Fact]
    public void Steps_that_name_nowhere_do_not_count_towards_the_half()
    {
        // Four steps, only two of them located; a place on both of those is on all of them.
        IReadOnlyList<uint>[] steps = [[], [10, 99], [], [20, 99]];

        Assert.Equal([99u], QuestReader.Furniture(steps));
    }

    [Fact]
    public void A_quest_that_names_one_place_a_step_has_no_furniture()
    {
        IReadOnlyList<uint>[] steps = [[10], [20], [30]];

        Assert.Empty(QuestReader.Furniture(steps));
    }

    [Fact]
    public void A_quest_with_no_located_steps_has_no_furniture()
    {
        Assert.Empty(QuestReader.Furniture([[], []]));
    }
}
