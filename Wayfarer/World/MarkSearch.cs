using Wayfarer.Routing;

namespace Wayfarer.World;

/// <summary>Which of the things standing in an area a step is about.
///
/// <para>A step that sends the player into a circle says where to search and never what for, so
/// the answer can only come from what is actually standing there. Each sort of mark is tried in
/// turn and the first sort that has anything standing there answers, so a broadly named sort can
/// never take an answer away from a narrowly named one. Within a sort, nearest to the player
/// wins.</para>
///
/// <para>Nothing here reads the game. It is given what is standing there and says which of it to
/// walk to, which is why a real circle full of real people can be written down and put to it.</para>
/// </summary>
public static class MarkSearch
{
    /// <summary>The sorts of mark, in the order a step means them.</summary>
    private static readonly MarkKind[] Sorts = [MarkKind.Thing, MarkKind.Person, MarkKind.Creature];

    /// <summary>Which thing to walk to, or null when none of what was named is standing there.
    ///
    /// <para>All the ground a step named is searched at once and the nearest answer taken, not the
    /// first. A step that sends the player to three people gives their three places in the order
    /// the sheet wrote them, which has nothing to do with where the player is standing.</para>
    /// </summary>
    /// <param name="areas">The ground to search. A place with no room in it is not searched.</param>
    /// <param name="standing">Everything the world holds nearby.</param>
    /// <param name="marks">What the step names, by id and sort.</param>
    /// <param name="owner">The event whose own spawns count as things to act on, or zero.</param>
    /// <param name="from">Where the player stands, which decides which of several is nearest.</param>
    public static Found? Choose(
        IReadOnlyList<Place> areas,
        IReadOnlyList<Candidate> standing,
        IReadOnlyList<Mark>? marks,
        uint owner,
        Place from)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentNullException.ThrowIfNull(from);

        var ground = areas.Where(area => area.Radius > 0f).ToList();
        if (ground.Count == 0)
        {
            return null;
        }

        foreach (var sort in Sorts)
        {
            if (Sort(ground, standing, marks, owner, from, sort) is { } nearest)
            {
                return nearest;
            }
        }

        return null;
    }

    /// <summary>Whether a step names this one: stamped by the game as the guided event's own, or
    /// named by the module. The stamp answers only for things to act on, which is what a step
    /// sends the player to before anything has been summoned, and never for people — the game does
    /// not stamp those.</summary>
    private static bool Wanted(Candidate candidate, IReadOnlyList<Mark> marks, uint owner, MarkKind sort)
    {
        if (sort is MarkKind.Thing && owner != 0 && candidate.Event == owner)
        {
            return true;
        }

        foreach (var mark in marks)
        {
            if (mark.Kind == sort && mark.Id == candidate.BaseId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The nearest thing of one sort standing inside the area.</summary>
    private static Found? Sort(
        IReadOnlyList<Place> areas,
        IReadOnlyList<Candidate> standing,
        IReadOnlyList<Mark>? marks,
        uint owner,
        Place from,
        MarkKind sort)
    {
        var named = marks ?? [];
        if (sort is not MarkKind.Thing && !named.Any(mark => mark.Kind == sort))
        {
            return null;
        }

        // Where a thing stands is asked before how far it is. Two places on different maps can
        // hold the very same numbers, and a step often names ground in a zone the player is not
        // standing in: without this, something underfoot answers for something a world away.
        var here = standing
            .Where(candidate => candidate.Targetable
                && Wanted(candidate, named, owner, sort)
                && areas.Any(area => area.Territory == candidate.At.Territory
                    && area.OnTheGround(candidate.At) <= area.Radius))
            .ToList();

        if (here.Count == 0)
        {
            return null;
        }

        // The game marks what it still wants of the player and unmarks it the moment it has had
        // it. Two of a kind can stand side by side, alike in every other way, one already helped
        // and one not, and this is the only thing that tells them apart — and unlike our own note
        // of what we have done, it is still true after the plugin is loaded again mid-errand.
        var asking = here.Where(candidate => candidate.Plate != 0).ToList();
        var wanted = asking.Count > 0 ? asking : here;

        var nearest = wanted.OrderBy(candidate => from.OnTheGround(candidate.At)).First();
        return new Found(nearest.Id, nearest.At);
    }
}
