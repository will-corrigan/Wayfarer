namespace Wayfarer.Core.Guidance;

/// <summary>Where an objective is, cut along what the router can actually do about it. A closed
/// union (the private constructor makes it uninheritable outside this file) rather than a
/// coordinate triple, because objectives are not uniformly positional: a quest step may have no
/// map location, a hunting target may live inside an instanced duty with no coordinates at all,
/// and a marker may be territory-correct but map-wrong.
///
/// Three deliberate omissions:
/// <list type="bullet">
/// <item>There is no <c>LiveEntity</c> case. A live-tracked mob is a <see cref="WorldPoint"/>
/// re-emitted every tick with the SAME <see cref="ObjectiveKey"/>; freshness is a property of
/// re-emission, not of the destination. "Advance when the mob dies" then falls out for free — the
/// source simply stops offering that key.</item>
/// <item><see cref="InstancedDuty"/> is a first-class case, not an error: it is what lets a
/// duty-gated hunting target stay in a chain instead of being silently dropped.</item>
/// <item>There is no nullable-everything flat record. That shape is exactly what produced
/// sentinel values like a quest row id of 0 on a hunting target.</item>
/// </list></summary>
public abstract record ObjectiveDestination
{
    private ObjectiveDestination()
    {
    }

    /// <summary>A usable world position.</summary>
    /// <param name="IsLive">Marks a position refreshed from the live object table this tick rather
    /// than a curated coordinate — DISPLAY ONLY ("42 yalms" vs "42 yalms (route)"). Routing treats
    /// both alike.</param>
    /// <param name="Radius">The game's own search-area radius, in yalms, or 0 for an ordinary point
    /// objective — see <see cref="Navigation.MarkerPoint.Radius"/> for where this comes from. Carried
    /// through so a "search this area" quest step never gets the same confident, precise arrow as a
    /// waypoint the game actually knows the exact position of.</param>
    public sealed record WorldPoint(
        uint Territory, uint MapId, float X, float Y, float Z, bool IsLive = false, float Radius = 0f)
        : ObjectiveDestination;

    /// <summary>Right zone known, no usable point in it: teleport/aethernet advice still applies,
    /// no arrow once you arrive.</summary>
    public sealed record TerritoryOnly(uint Territory, uint? MapId) : ObjectiveDestination;

    /// <summary>Inside instanced content: no aetherytes, no entrances, nothing to route to — the
    /// router answers with duty guidance (queue it / unlock it) instead of a useless "no route
    /// found".</summary>
    public sealed record InstancedDuty(uint DutyTerritory) : ObjectiveDestination;

    /// <summary>Knows what, not where. <paramref name="Reason"/> is the player-facing sentence,
    /// owned by the source.</summary>
    public sealed record Unresolved(string Reason) : ObjectiveDestination;
}
