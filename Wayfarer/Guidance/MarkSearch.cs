using Wayfarer.Routing;

namespace Wayfarer.Guidance;

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

    /// <summary>Which thing to walk to, and whether anything of the step's was standing there at
    /// all. Nothing standing there and nothing tried are different answers: the first means the
    /// circle is still the best that can be said, the second means going round them again.</summary>
    /// <param name="area">The circle to search. A place with no room in it is not searched.</param>
    /// <param name="standing">Everything the world holds nearby.</param>
    /// <param name="marks">What the step names, by id and sort.</param>
    /// <param name="owner">The event whose own spawns count as things to act on, or zero.</param>
    /// <param name="from">Where the player stands, which decides which of several is nearest.</param>
    /// <param name="tried">What the player has already acted on and need not be sent back to.</param>
    public static (Place? Nearest, bool AnyPresent) Choose(
        Place area,
        IReadOnlyList<Candidate> standing,
        IReadOnlyList<Mark>? marks,
        uint owner,
        Place from,
        Func<uint, bool>? tried = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentNullException.ThrowIfNull(from);

        if (area.Radius <= 0f)
        {
            return (null, false);
        }

        var any = false;
        foreach (var sort in Sorts)
        {
            var (nearest, present) = Sort(area, standing, marks, owner, from, tried, sort);
            any |= present;
            if (nearest is not null)
            {
                return (nearest, true);
            }
        }

        return (null, any);
    }

    /// <summary>How far apart two points are across the ground. Heights in the routing data are
    /// flat, so counting the drop would push something on a ledge out of a circle it is plainly
    /// standing in.</summary>
    private static float OnTheGround(Place from, Place to)
    {
        var (dx, dz) = (from.X - to.X, from.Z - to.Z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
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

    /// <summary>The nearest untried thing of one sort standing inside the area, and whether any of
    /// that sort was standing there at all.</summary>
    private static (Place? Nearest, bool AnyPresent) Sort(
        Place area,
        IReadOnlyList<Candidate> standing,
        IReadOnlyList<Mark>? marks,
        uint owner,
        Place from,
        Func<uint, bool>? tried,
        MarkKind sort)
    {
        var named = marks ?? [];
        if (sort is not MarkKind.Thing && !named.Any(mark => mark.Kind == sort))
        {
            return (null, false);
        }

        var here = standing
            .Where(candidate => candidate.Targetable
                && Wanted(candidate, named, owner, sort)
                && OnTheGround(area, candidate.At) <= area.Radius)
            .ToList();

        if (here.Count == 0)
        {
            return (null, false);
        }

        // The game marks what it still wants of the player and unmarks it the moment it has had
        // it. Two of a kind can stand side by side, alike in every other way, one already helped
        // and one not, and this is the only thing that tells them apart — and unlike our own note
        // of what we have done, it is still true after the plugin is loaded again mid-errand.
        var asking = here.Where(candidate => candidate.Plate != 0).ToList();
        var wanted = asking.Count > 0 ? asking : here;

        var untried = wanted.Where(candidate => tried?.Invoke(candidate.BaseId) is not true).ToList();
        if (untried.Count == 0)
        {
            return (null, true);
        }

        var nearest = untried.OrderBy(candidate => OnTheGround(from, candidate.At)).First();
        return (nearest.At, true);
    }
}
