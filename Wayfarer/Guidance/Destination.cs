using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Where an entry's target is. Exactly three kinds, because routing treats exactly three
/// things differently: it runs the graph over places, it offers the Duty Finder for a duty, and
/// it does nothing for something that cannot be reached. Only routing switches on this; surfaces
/// read the words and the route instead.
///
/// <para>Each kind carries only its own fields, so a module cannot produce a duty with a radius or
/// a place with a queue id. Adding a kind is only right when routing would treat it differently
/// from all three of these.</para></summary>
public abstract record Destination
{
    private Destination()
    {
    }

    /// <summary>Somewhere the player can go. Several places when the entry can be completed at
    /// any of them — six roses, three loaded karakul — and routing picks the cheapest to reach from
    /// wherever the player is standing this frame.</summary>
    public sealed record Reachable(IReadOnlyList<Place> Places) : Destination;

    /// <summary>Inside instanced content. Nothing to walk to; the only guidance is to queue.</summary>
    public sealed record InDuty(uint DutyId) : Destination;

    /// <summary>Cannot be reached now, and here is why: a gate the player has not passed, or a
    /// gap in the data. Routing does nothing; the words say the reason.</summary>
    public sealed record Blocked(string Reason) : Destination;
}
