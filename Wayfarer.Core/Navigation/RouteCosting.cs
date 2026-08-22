namespace Wayfarer.Core.Navigation;

/// <summary>The three ways to reach an objective outside the player's current map:
/// hop the shared city-wide aethernet network, walk through a physical map-link door,
/// or teleport. Mutually exclusive — <see cref="RouteCosting.Choose"/> ranks all built
/// candidates by <see cref="RouteCandidate.Cost"/> and keeps the cheapest.</summary>
public enum RouteMode
{
    Aethernet,
    Entrance,
    Teleport,
}

/// <summary>A map-link marker (door / zone exit) between two maps, in the source map's
/// coordinate space. Built by the plugin from <c>MapMarker</c> DataType 1/2 rows.</summary>
public sealed record MapLinkPoint(string Name, float X, float Z);

/// <summary>One costed way to reach a cross-zone objective. <see cref="ArrowX"/>/<see
/// cref="ArrowZ"/> are in the PLAYER's current-territory coordinate space and are null
/// for <see cref="RouteMode.Teleport"/> (nothing to point an in-zone arrow at). The
/// Aethernet*/Entrance*/Aetheryte* fields are populated only for their own mode; the
/// plugin copies whichever set is non-null onto NavigationState.</summary>
public sealed record RouteCandidate(
    RouteMode Mode,
    float Cost,
    float? ArrowX,
    float? ArrowZ,
    string? AethernetEntryName = null,
    string? AethernetExitName = null,
    string? EntranceName = null,
    float? RemainingYalms = null,
    uint? AetheryteId = null,
    string? AetheryteName = null,
    bool AetheryteUnlocked = false);

/// <summary>Pure candidate costing/selection for intra-city travel. Coordinate spaces
/// are per-territory (never compare X/Z across two different AetherytePoint.Territory
/// values) — callers pass in shard/link lists already scoped to a single territory or
/// map on each side of a leg.</summary>
public static class RouteCosting
{
    // Menu-interaction / loading-hop overhead for the aethernet leg; reused from
    // AetherytePicker's same-zone slack so both use one tuned constant.
    public const float AethernetSlack = AetherytePicker.RouteSlack;

    // Teleport costs a full loading screen versus an aethernet hop's short one —
    // roughly double the aethernet slack (tunable).
    public const float TeleportOverhead = AethernetSlack * 2f;

    /// <summary>Hop the shared aethernet network: nearest shard to the player in the
    /// current territory, out the nearest shard to the objective in the target
    /// territory. Null when either side has no shard, the two shards are not on the
    /// same AethernetGroup (i.e. the cities aren't linked by one network), or the
    /// nearest shard on both ends is the SAME shard — a same-shard "hop" is not a
    /// route, and this guard matters now that a caller can pass the same territory's
    /// shard list on both sides (same-territory, different-map objectives routed via
    /// QuestNavigator's MarkerMatch.TerritoryOnly path), where player and objective
    /// can otherwise resolve to one shared nearest shard.</summary>
    public static RouteCandidate? AethernetCandidate(
        IReadOnlyList<AetherytePoint> currentTerritoryShards,
        IReadOnlyList<AetherytePoint> targetTerritoryShards,
        float px,
        float pz,
        float tx,
        float tz)
    {
        if (AetherytePicker.Nearest(currentTerritoryShards, px, pz) is not { } entry
            || AetherytePicker.Nearest(targetTerritoryShards, tx, tz) is not { } exit
            || entry.Id == exit.Id || entry.Group == 0 || entry.Group != exit.Group)
        {
            return null;
        }

        var playerToEntry = NavMath.Distance(entry.X - px, 0, entry.Z - pz);
        var exitToObjective = NavMath.Distance(exit.X - tx, 0, exit.Z - tz);

        return new(
            RouteMode.Aethernet,
            playerToEntry + exitToObjective + AethernetSlack,
            entry.X,
            entry.Z,
            AethernetEntryName: entry.Name,
            AethernetExitName: exit.Name,
            RemainingYalms: exitToObjective);
    }

    /// <summary>Walk through a physical map-link door: nearest door to the player on
    /// the current map, then (after crossing) the nearest reciprocal door to the
    /// objective on the target map. Null when either map has no map-link toward the
    /// other.</summary>
    public static RouteCandidate? EntranceCandidate(
        IReadOnlyList<MapLinkPoint> sourceLinks,
        IReadOnlyList<MapLinkPoint> targetLinks,
        float px,
        float pz,
        float tx,
        float tz)
    {
        MapLinkPoint? bestSource = null;
        var bestSourceDist = float.MaxValue;
        foreach (var l in sourceLinks)
        {
            var d = NavMath.Distance(l.X - px, 0, l.Z - pz);
            if (d < bestSourceDist)
            {
                bestSourceDist = d;
                bestSource = l;
            }
        }

        var bestTargetDist = float.MaxValue;
        foreach (var l in targetLinks)
        {
            var d = NavMath.Distance(l.X - tx, 0, l.Z - tz);
            if (d < bestTargetDist)
            {
                bestTargetDist = d;
            }
        }

        if (bestSource is not { } source || bestTargetDist == float.MaxValue)
        {
            return null;
        }

        return new(
            RouteMode.Entrance,
            bestSourceDist + bestTargetDist,
            source.X,
            source.Z,
            EntranceName: source.Name,
            RemainingYalms: bestTargetDist);
    }

    /// <summary>Teleport to the given aetheryte (the target territory's own, or the
    /// TerritoryType fallback for territories that own none). Null when there is no
    /// aetheryte, when the aetheryte's OWN territory is where the player already
    /// stands (never recommend teleporting to here; this is the split-city
    /// TerritoryType.Aetheryte fallback bug the evidence block calls out), or when the
    /// aetheryte's AethernetGroup matches any group already reachable from the
    /// player's current territory — a city-network-local "teleport" is never useful
    /// advice (a full loading screen to reach a stop the free aethernet already
    /// covers; e.g. Foundation aetheryte suggested while standing in The Pillars —
    /// both group 4 in the live Ishgard split city). Cross-city teleports (different
    /// AethernetGroup, or group 0 = no network at all, e.g. an overworld/field
    /// aetheryte) are unaffected. When the aetheryte sits in neither the current nor
    /// the target territory (a third territory's fallback), cost is <see
    /// cref="float.MaxValue"/> — a sentinel that makes this candidate lose to any
    /// other route and only survive if it's the only one on offer, rather than a real
    /// distance-based cost.</summary>
    public static RouteCandidate? TeleportCandidate(
        AetherytePoint? aetheryte,
        uint aetheryteTerritory,
        uint targetTerritory,
        uint currentTerritory,
        float tx,
        float tz,
        bool unlocked,
        IReadOnlyCollection<uint> currentTerritoryAethernetGroups)
    {
        if (aetheryte is not { } a || aetheryteTerritory == currentTerritory)
        {
            return null;
        }

        if (a.Group != 0 && currentTerritoryAethernetGroups.Contains(a.Group))
        {
            return null;
        }

        var cost = aetheryteTerritory == targetTerritory
            ? TeleportOverhead + NavMath.Distance(a.X - tx, 0, a.Z - tz)
            : float.MaxValue;

        return new(
            RouteMode.Teleport,
            cost,
            null,
            null,
            AetheryteId: a.Id,
            AetheryteName: a.Name,
            AetheryteUnlocked: unlocked);
    }

    /// <summary>Picks the cheapest non-null candidate; null when none were buildable.</summary>
    public static RouteCandidate? Choose(params RouteCandidate?[] candidates)
    {
        RouteCandidate? best = null;
        foreach (var c in candidates)
        {
            if (c != null && (best == null || c.Cost < best.Cost))
            {
                best = c;
            }
        }

        return best;
    }
}
