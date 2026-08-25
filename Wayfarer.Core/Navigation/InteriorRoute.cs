namespace Wayfarer.Core.Navigation;

/// <summary>Where an interior territory's door is — in the coordinate space of the OUTDOOR
/// territory whose map draws it, which is neither the player's territory nor the objective's.
///
/// <para>This type exists because the router used to model only TWO places: where the player is
/// and where the objective is. An interior objective in a city has THREE — the player's
/// territory, the door's territory, and the interior itself — and the third one is the only one
/// with usable coordinates. An interior territory owns no aetheryte rows at all and its own map
/// carries no markers, so nothing about it can be costed on its own terms; its world coordinates
/// are in a private space that cannot be compared with the player's. The single thing the game
/// data does say about where it is, is the place-name label the enclosing city map draws on its
/// door (verified in the live sheet: Fortemps Manor is territory 433, map 222, MapMarkerRange 0,
/// no aetheryte rows homed in it — and the only reference to it anywhere is a MapMarker of
/// DataType 0 on map 219, The Pillars, whose PlaceNameSubtext is territory 433's own PlaceName
/// row). Represent that door and the route composes: shard across the city to the half that
/// holds it, then walk in.</para></summary>
/// <param name="HostTerritory">The outdoor territory the door stands in — whose coordinate space
/// <see cref="X"/>/<see cref="Z"/> belong to.</param>
/// <param name="Name">The interior's own place name, as the city map labels the door.</param>
public sealed record InteriorEntrance(uint HostTerritory, uint HostMapId, string Name, float X, float Z);

/// <summary>Costs the ways to reach an interior objective's door. Pure; every leg is built from
/// the same <see cref="RouteCosting"/> primitives an ordinary cross-zone objective uses, so an
/// interior objective is not a special case with its own rules — it is an ordinary route to a
/// place the router previously had no way to name.</summary>
public static class InteriorRoute
{
    /// <summary>Close enough to the door that no route is worth offering — the player can see it.
    /// Inside this radius <see cref="Route"/> deliberately returns null so the caller falls
    /// through to <see cref="OtherZoneResolution.InteriorMessage"/>: "find the entrance" is only
    /// useful advice when the entrance is actually in front of you. That message firing from the
    /// other side of the city is the defect this radius exists to bound.</summary>
    public const float AtEntranceYalms = 25f;

    /// <summary>Whether the player is standing at the door — which requires being in the door's
    /// own territory, since positions in two different territories are not comparable.</summary>
    public static bool AtEntrance(InteriorEntrance entrance, uint currentTerritory, float px, float pz)
    {
        ArgumentNullException.ThrowIfNull(entrance);
        return entrance.HostTerritory == currentTerritory
            && NavMath.Distance(entrance.X - px, 0, entrance.Z - pz) <= AtEntranceYalms;
    }

    /// <summary>The cheapest way to reach <paramref name="entrance"/>, or null when the player is
    /// already at it (nothing to route) or no leg is buildable (no shared network, no map-link).
    ///
    /// <para>Three legs, ranked by cost like any other candidate set:</para>
    /// <list type="bullet">
    /// <item><description><b>Aethernet</b> — the city network, scored from the player to their
    /// nearest shard and from the door's nearest shard to the door. This is the leg the reported
    /// case needs and the one that could never exist before: it is scored against the DOOR's
    /// position in the door's territory, not the interior's own unreachable
    /// coordinates.</description></item>
    /// <item><description><b>Walk</b> — only when the player is already in the door's territory,
    /// because only then are the two positions in one coordinate space.</description></item>
    /// <item><description><b>Map-link</b> — a physical door from the player's current map into the
    /// door's map, for an objective whose city the player is not in at all.</description></item>
    /// </list>
    ///
    /// <para><c>entranceTerritoryShards</c> are those homed in the door's own host territory. It
    /// may be the SAME list as <c>currentTerritoryShards</c> when the player already stands in that
    /// territory — <see cref="RouteCosting.AethernetCandidate"/> guards the degenerate case where
    /// both ends then resolve to one shared shard.</para></summary>
    public static RouteCandidate? Route(
        InteriorEntrance entrance,
        uint currentTerritory,
        float px,
        float pz,
        IReadOnlyList<AetherytePoint> currentTerritoryShards,
        IReadOnlyList<AetherytePoint> entranceTerritoryShards,
        IReadOnlyList<MapLinkPoint> currentMapLinks,
        IReadOnlyList<MapLinkPoint> entranceMapLinks)
    {
        ArgumentNullException.ThrowIfNull(entrance);
        if (AtEntrance(entrance, currentTerritory, px, pz))
        {
            return null;
        }

        var aethernet = RouteCosting.AethernetCandidate(
            currentTerritoryShards, entranceTerritoryShards, px, pz, entrance.X, entrance.Z);

        RouteCandidate? walk = null;
        if (entrance.HostTerritory == currentTerritory)
        {
            var direct = NavMath.Distance(entrance.X - px, 0, entrance.Z - pz);
            walk = new(RouteMode.Entrance, direct, entrance.X, entrance.Z, EntranceName: entrance.Name);

            // Within one territory a shard hop has to earn its travel menu and loading screen
            // against a walk the player could simply make — the same judgement the in-zone router
            // already applies to an ordinary objective, asked here of an already-costed candidate
            // so this does not become a second opinion about the same trade-off.
            if (aethernet is { } hop && !AetherytePicker.ShouldRouteViaAethernet(direct, hop.Cost))
            {
                aethernet = null;
            }
        }

        var link = RouteCosting.EntranceCandidate(
            currentMapLinks, entranceMapLinks, px, pz, entrance.X, entrance.Z);

        return RouteCosting.Choose(aethernet, walk, link);
    }
}
