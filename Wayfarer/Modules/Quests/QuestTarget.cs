using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Wayfarer.Guidance;
using Wayfarer.Routing;
using Wayfarer.World;

namespace Wayfarer.Modules.Quests;

/// <summary>Turns what a step says into somewhere to go, which is the quests module's own job and
/// nobody else's.
///
/// <para>A step gives places, and some of those are ground to search rather than a point to stand
/// on. Ground says where to look and never what for, so what the step is about can only be settled
/// by looking: the quest names its own objects, its own people and its own creatures, and the game
/// stamps what it spawned. When one of them is standing there, that one thing is the answer and
/// the ground was only ever the way to find it. When none is, the ground is still the answer.</para>
///
/// <para>Asked afresh every frame, because the answer moves: people walk, things are used up and
/// disappear, and what the game marks as still wanting the player changes as the player deals with
/// them. Nothing downstream re-decides any of this — it is given one thing and follows it.</para>
/// </summary>
internal sealed class QuestTarget(IObjectFinder finder, IInteractions dealtWith)
{
    private IReadOnlyList<Place> last = [];
    private IReadOnlyList<Mark>? lastMarks;
    private EventId? lastOwner;

    /// <summary>Where a step sends the player: the one thing of the quest's standing in the ground
    /// it named, or the ground itself.</summary>
    /// <param name="places">Everywhere the step says, ground and points alike.</param>
    /// <param name="marks">What the quest names, by the id the world gives it and its sort.</param>
    /// <param name="owner">The quest as the game's event system knows it, whose own spawns count
    /// without being named.</param>
    public Destination Where(IReadOnlyList<Place> places, IReadOnlyList<Mark>? marks, EventId? owner, IReadOnlyList<Place>? lairs = null)
    {
        ArgumentNullException.ThrowIfNull(places);

        (last, lastMarks, lastOwner) = (places, Left(marks), owner);
        if (Look(places, lastMarks, owner) is { } found)
        {
            return new Destination.AtObject(found.Id, found.At);
        }

        // Nothing of the quest's is standing there. A step that sends the player to wide ground
        // often means something that is not there until something else has been done, and the data
        // says where those stand even while they do not: better to walk to where they will be than
        // to the middle of a circle, which means nothing at all.
        var awaited = Awaited(places, lairs);
        return new Destination.Reachable(awaited.Count > 0 ? awaited : places);
    }

    /// <summary>Forgets what has been dealt with, because the step moved on and what was tried for
    /// the last one says nothing about this one.</summary>
    public void Begin() => dealtWith.Forget();

    /// <summary>Which thing the step is about this very moment, asked again of the places last
    /// settled on. The answer moves while the words do not — someone is helped and the next one
    /// becomes the nearest, a thing is used up and goes — so this is what says the objective has
    /// to be made again even though the step has not changed. Zero when nothing of the quest's is
    /// standing in them.</summary>
    public ulong Aim() => Look(last, lastMarks, lastOwner)?.Id ?? 0uL;

    /// <summary>Where the quest's creatures are known to stand, kept to the ground the step
    /// named. Empty when the data says nowhere, or nowhere inside it.</summary>
    private static IReadOnlyList<Place> Awaited(IReadOnlyList<Place> places, IReadOnlyList<Place>? lairs)
    {
        if (lairs is null || lairs.Count == 0)
        {
            return [];
        }

        return [.. lairs.Where(lair => places.Any(place => place.Radius > 0f
            && place.Territory == lair.Territory
            && place.OnTheGround(lair) <= place.Radius))];
    }

    /// <summary>What is still worth naming: everything the quest named, less what the player has
    /// already acted on for this step.
    ///
    /// <para>Three patches of soil stand in a circle and only one has anything under it. They are
    /// the same in the sheet, the same in the world, and the game marks none of them, so the only
    /// thing that tells the dug one from the rest is having watched the player dig it. Naming two
    /// instead of three is how the next one is walked to.</para></summary>
    private IReadOnlyList<Mark>? Left(IReadOnlyList<Mark>? marks) =>
        marks is null ? null : [.. marks.Where(mark => !dealtWith.Tried(mark.Id))];

    /// <summary>The one thing of the quest's standing in any of the step's ground, nearest first.
    /// All of it is asked about at once: a step naming three people gives their places in the
    /// order the sheet wrote them, and walking to the first of those rather than the nearest sends
    /// the player past two of them to reach the third.</summary>
    private Found? Look(IReadOnlyList<Place> places, IReadOnlyList<Mark>? marks, EventId? owner) =>
        finder.Inside(places, marks, owner, dealtWith.Tried);
}
