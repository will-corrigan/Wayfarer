using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Where an entry's target is. Exactly four kinds, because routing treats exactly four
/// things differently: it runs the graph over places, it follows one thing standing in the world,
/// it offers the Duty Finder for a duty, and it does nothing for something that cannot be reached.
///
/// <para>Each kind carries only its own fields, so a module cannot produce a duty with a radius or
/// a place with a queue id. Nothing here says how a module decided any of it: which of a step's
/// places are the step's, and which of the things standing in one is the one, are the module's own
/// questions, answered before anything is published.</para></summary>
public abstract record Destination
{
    private Destination()
    {
    }

    /// <summary>Somewhere the player can go. Several places when the entry can be done at any of
    /// them — six roses, three loaded karakul — and routing picks the cheapest to reach from
    /// wherever the player is standing.</summary>
    /// <param name="Places">Where the entry can be done, each with its radius when it is ground to
    /// search rather than a point to stand on.</param>
    public sealed record Reachable(IReadOnlyList<Place> Places) : Destination;

    /// <summary>One particular thing standing in the world, which the module picked out of
    /// everything that was there. Routing walks to it and the needle follows it as it moves.</summary>
    /// <param name="Id">The one the world gives it, so it is still the same thing a moment later.</param>
    /// <param name="At">Where it stood when it was picked, and where to go if it is no longer
    /// loaded.</param>
    public sealed record AtObject(ulong Id, Place At) : Destination;

    /// <summary>Inside instanced content. Nothing to walk to; the only guidance is to queue.</summary>
    public sealed record InDuty(uint DutyId) : Destination;

    /// <summary>Cannot be reached now, and here is why: a gate the player has not passed, or a
    /// gap in the data. Routing does nothing; the words say the reason.</summary>
    public sealed record Blocked(string Reason) : Destination;
}
