using Wayfarer.Modules.Quests;
using Wayfarer.World;

namespace Wayfarer.Tests;

/// <summary>Which of the things a quest names belong to the step the player is on, from the quest's
/// own listeners. The shapes are "Put to the Proof"'s: three statues found in turn among decoys.</summary>
public class StepMarksTests
{
    private const uint Serpent = 2009879;
    private const uint OpoOpo = 2009880;
    private const uint Coeurl = 2009881;
    private const uint Wolf = 2009882;
    private const uint Colibri = 2009883;
    private const byte For = 255;

    private static readonly Mark[] Statues = [Thing(Serpent), Thing(OpoOpo), Thing(Coeurl), Thing(Wolf), Thing(Colibri), new(1029223, MarkKind.Person)];

    private static readonly (uint, byte, byte)[] Listeners =
    [
        (Serpent, 2, 1),
        (OpoOpo, 3, 2), (Coeurl, 3, For),
        (Wolf, 4, 3), (Colibri, 4, For),
    ];

    [Fact]
    public void The_third_statue_is_the_wolf_not_the_decoy_beside_it_nor_an_earlier_statue()
    {
        var marks = StepMarks.For(Statues, Listeners, 4);

        Assert.Contains(Thing(Wolf), marks);
        Assert.DoesNotContain(Thing(Colibri), marks);
        Assert.DoesNotContain(Thing(Serpent), marks);
        Assert.DoesNotContain(Thing(OpoOpo), marks);
    }

    [Fact]
    public void People_and_creatures_are_left_as_they_were()
    {
        Assert.Contains(new Mark(1029223, MarkKind.Person), StepMarks.For(Statues, Listeners, 4));
    }

    [Fact]
    public void Of_three_patches_of_soil_the_one_the_step_ties_to_itself_is_pointed_at()
    {
        // The Honest Truth: "Search Bittermill for evidence." Three patches of barren soil, and only
        // one is listed for the step alone; the other two are listed as lasting.
        Mark[] soil = [Thing(2008760), Thing(2008761), Thing(2008762)];
        (uint, byte, byte)[] listened = [(2008760, 5, For), (2008761, 5, 4), (2008762, 5, For)];

        Assert.Equal([Thing(2008761)], StepMarks.For(soil, listened, 5));
    }

    [Fact]
    public void A_step_that_lists_nothing_of_its_own_is_taken_as_it_was()
    {
        Assert.Equal(Statues, StepMarks.For(Statues, Listeners, 9));
    }

    /// <summary>"Mi Casa, Toupasa": searching a wide circle for the owl statuette, which the step
    /// ties to itself; the altar belongs to the next step, the unnamed marker lasts all quest.</summary>
    [Fact]
    public void A_search_owns_the_statuette_it_is_for_and_nothing_else()
    {
        Mark[] named = [Thing(2010219), Thing(2010218), Thing(2010217)];
        (uint, byte, byte)[] listened = [(2010219, 2, For), (2010218, 2, 1), (2010217, 3, 2), (2010218, 3, 2)];

        Assert.Equal([2010218u], StepMarks.Own(named, listened, 2));
    }

    private static Mark Thing(uint id) => new(id, MarkKind.Thing);
}
