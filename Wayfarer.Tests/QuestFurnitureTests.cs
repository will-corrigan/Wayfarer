using Wayfarer.Modules.Quests;

namespace Wayfarer.Tests;

/// <summary>Which of a quest's places belong to the quest rather than to any one of its steps.
/// The fixtures are the shapes the quest sheet really has, measured across every quest in the
/// game: a door hung on five steps of six, a portal on eight of nine, an errand that comes round
/// twice out of seven, and the people a quest sends you back to again and again.</summary>
public class QuestFurnitureTests
{
    /// <summary>The row id of a thing, as opposed to a person.</summary>
    private const uint Door = 99;

    private static readonly HashSet<uint> Things = [Door, 98];

    [Fact]
    public void A_thing_on_more_than_half_a_quests_steps_belongs_to_the_quest()
    {
        // "Heavens Weep": the sealed door on every step after the first, each step's own place once.
        IReadOnlyList<uint>[] steps = [[10], [20, Door], [30, Door], [40, Door], [50, Door], [60, Door]];

        Assert.Equal([Door], QuestReader.Furniture(steps, Things));
    }

    [Fact]
    public void A_person_a_quest_sends_you_back_to_is_never_furniture()
    {
        // The same NPC on every step. A quest does this on purpose and the step still means them.
        IReadOnlyList<uint>[] steps = [[10, 7], [20, 7], [30, 7], [40, 7]];

        Assert.Empty(QuestReader.Furniture(steps, Things));
    }

    [Fact]
    public void A_thing_on_exactly_half_the_steps_is_left_alone()
    {
        IReadOnlyList<uint>[] steps = [[10, Door], [20, Door], [30], [40]];

        Assert.Empty(QuestReader.Furniture(steps, Things));
    }

    [Fact]
    public void A_thing_a_quest_names_on_one_step_only_is_left_alone()
    {
        // One located step naming a door is on all of its steps, which means nothing.
        IReadOnlyList<uint>[] steps = [[10, Door]];

        Assert.Empty(QuestReader.Furniture(steps, Things));
    }

    [Fact]
    public void An_errand_that_comes_round_again_is_not_furniture()
    {
        IReadOnlyList<uint>[] steps = [[10], [Door], [30], [40], [50], [60], [Door]];

        Assert.Empty(QuestReader.Furniture(steps, Things));
    }

    [Fact]
    public void Steps_that_name_nowhere_do_not_count_towards_the_half()
    {
        IReadOnlyList<uint>[] steps = [[], [10, Door], [], [20, Door]];

        Assert.Equal([Door], QuestReader.Furniture(steps, Things));
    }

    [Fact]
    public void Two_things_carried_through_are_both_furniture()
    {
        IReadOnlyList<uint>[] steps = [[10, 98, Door], [20, 98, Door], [30, 98, Door]];

        Assert.Equal([98u, Door], QuestReader.Furniture(steps, Things).OrderBy(row => row));
    }

    [Fact]
    public void A_quest_with_no_located_steps_has_no_furniture()
    {
        Assert.Empty(QuestReader.Furniture([[], []], Things));
    }
}
