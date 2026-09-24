using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Wayfarer.Guidance;
using Wayfarer.Modules.Quests;
using Wayfarer.Routing;
using Wayfarer.World;

namespace Wayfarer.Tests;

/// <summary>What the quests module makes of a step: one thing of the quest's standing in the
/// ground it named, or the ground itself. What it has already dealt with is its own to remember,
/// and it stops naming those.</summary>
public class QuestTargetTests
{
    private static readonly Place Circle = new(620, 368, 0f, 0f, 0f, 35f);
    private static readonly Place FirstSoil = new(620, 368, 10f, 0f, 10f);
    private static readonly Place SecondSoil = new(620, 368, 20f, 0f, 20f);
    private static readonly Place Lair = new(620, 368, 5f, 0f, 5f);

    private static readonly Mark[] ThreePatches =
        [new(101, MarkKind.Thing), new(102, MarkKind.Thing), new(103, MarkKind.Thing)];

    [Fact]
    public void One_of_the_quests_own_things_standing_there_is_where_the_step_sends_you()
    {
        var finder = new Finder { Answer = new Found(7, FirstSoil) };
        var target = new QuestTarget(finder, new Memory());

        var where = target.Where([Circle], ThreePatches, null);

        var thing = Assert.IsType<Destination.AtObject>(where);
        Assert.Equal(7uL, thing.Id);
        Assert.Equal(FirstSoil, thing.At);
    }

    [Fact]
    public void Soil_already_dug_is_not_named_again()
    {
        // Three patches of soil, identical in the sheet and in the world, and the game marks none
        // of them. Having watched the player dig one is the only thing that tells it from the
        // rest, so the next time only the other two are named.
        var finder = new Finder { Answer = new Found(7, SecondSoil) };
        var memory = new Memory();
        var target = new QuestTarget(finder, memory);

        memory.Dug(102);
        target.Where([Circle], ThreePatches, null);

        Assert.Equal([101u, 103u], finder.Asked.Select(mark => mark.Id));
    }

    [Fact]
    public void What_was_already_tried_is_passed_over_even_when_the_game_spawned_it()
    {
        // The finder also answers with what the game stamped as the quest's own, which the names
        // never reach, so what was tried has to go to the finder itself.
        var finder = new Finder();
        var memory = new Memory();
        var target = new QuestTarget(finder, memory);

        memory.Dug(2009058);
        target.Where([Circle], [], null);

        Assert.True(finder.PassedOver?.Invoke(2009058));
        Assert.False(finder.PassedOver?.Invoke(2009059));
    }

    [Fact]
    public void Beginning_a_step_forgets_what_the_last_one_dealt_with()
    {
        var memory = new Memory();
        var target = new QuestTarget(new Finder(), memory);
        memory.Dug(102);

        target.Begin();

        Assert.Equal(0, memory.Count);
    }

    [Fact]
    public void Nothing_of_the_quests_standing_there_leaves_the_ground_itself()
    {
        var target = new QuestTarget(new Finder(), new Memory());

        var where = target.Where([Circle], ThreePatches, null);

        Assert.Equal([Circle], Assert.IsType<Destination.Reachable>(where).Places);
    }

    [Fact]
    public void Nothing_standing_there_yet_is_walked_to_where_it_will_stand()
    {
        var target = new QuestTarget(new Finder(), new Memory());

        var where = target.Where([Circle], ThreePatches, null, [Lair]);

        Assert.Equal([Lair], Assert.IsType<Destination.Reachable>(where).Places);
    }

    [Fact]
    public void Somewhere_the_things_stand_that_is_not_in_the_ground_named_is_not_offered()
    {
        Place elsewhere = new(620, 368, 400f, 0f, 400f);
        var target = new QuestTarget(new Finder(), new Memory());

        var where = target.Where([Circle], ThreePatches, null, [elsewhere]);

        Assert.Equal([Circle], Assert.IsType<Destination.Reachable>(where).Places);
    }

    private sealed class Finder : IObjectFinder
    {
        public Found? Answer { get; set; }

        public IReadOnlyList<Mark> Asked { get; private set; } = [];

        public Func<uint, bool>? PassedOver { get; private set; }

        public Found? Inside(IReadOnlyList<Place> areas, IReadOnlyList<Mark>? marks, EventId? owner, Func<uint, bool>? passedOver = null)
        {
            Asked = marks ?? [];
            PassedOver = passedOver;
            return Answer;
        }
    }

    private sealed class Memory : IInteractions
    {
        private readonly HashSet<uint> dug = [];

        public int Count => dug.Count;

        public void Dug(uint baseId) => dug.Add(baseId);

        public bool Tried(uint baseId) => dug.Contains(baseId);

        public void Forget() => dug.Clear();
    }
}
