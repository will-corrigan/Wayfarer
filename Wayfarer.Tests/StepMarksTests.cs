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

    private static readonly Dictionary<uint, string> Names = new()
    {
        [Serpent] = "serpent statue", [OpoOpo] = "opo-opo statue", [Coeurl] = "coeurl statue", [Wolf] = "wolf statue", [Colibri] = "colibri statue",
    };

    [Fact]
    public void The_third_statue_is_the_wolf_not_the_decoy_beside_it_nor_an_earlier_statue()
    {
        var marks = StepMarks.For(Statues, Listeners, 4, Name);

        Assert.Contains(Thing(Wolf), marks);
        Assert.DoesNotContain(Thing(Colibri), marks);
        Assert.DoesNotContain(Thing(Serpent), marks);
        Assert.DoesNotContain(Thing(OpoOpo), marks);
    }

    [Fact]
    public void People_and_creatures_are_left_as_they_were()
    {
        Assert.Contains(new Mark(1029223, MarkKind.Person), StepMarks.For(Statues, Listeners, 4, Name));
    }

    [Fact]
    public void A_lasting_thing_like_the_steps_own_stays()
    {
        // Axe in the Stone: four solid rocks, one of them listed as lasting.
        Mark[] rocks = [Thing(1), Thing(2), Thing(3), Thing(4)];
        (uint, byte, byte)[] listened = [(1, 3, 2), (2, 3, 2), (3, 3, 2), (4, 3, For)];

        Assert.Equal(rocks, StepMarks.For(rocks, listened, 3, _ => "solid rock"));
    }

    [Fact]
    public void A_step_that_lists_nothing_of_its_own_is_taken_as_it_was()
    {
        Assert.Equal(Statues, StepMarks.For(Statues, Listeners, 9, Name));
    }

    private static Mark Thing(uint id) => new(id, MarkKind.Thing);

    private static string Name(uint id) => Names.GetValueOrDefault(id, string.Empty);
}
