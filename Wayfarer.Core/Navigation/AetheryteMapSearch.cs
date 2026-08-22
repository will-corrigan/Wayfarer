namespace Wayfarer.Core.Navigation;

/// <summary>Decides which map(s) to scan for an aetheryte's map markers when resolving
/// its position. Exists because an Aetheryte row's own Map reference is not reliable:
/// in live sheet data every Ishgard shard row (80–82 Foundation, 83–87 The Pillars)
/// carries Map=0 and dead Level refs, while the TERRITORY's map (218/219) does carry
/// the shard markers (MapMarker DataType 4 keyed by AethernetName) — verified against
/// the game's own sqpack, 2026-08-22. Searching only the row's map meant those shards
/// never resolved a position, the point builder dropped them, and intra-Ishgard
/// aethernet routing silently never produced a single candidate.</summary>
public static class AetheryteMapSearch
{
    /// <summary>Map row ids to scan for the aetheryte's marker, in preference order:
    /// the row's own map first, then the home territory's map as a fallback. Zero
    /// (missing) references are skipped and duplicates collapsed; an empty result
    /// means the position is unresolvable from map markers.</summary>
    public static IReadOnlyList<uint> CandidateMaps(uint rowMap, uint territoryMap)
    {
        if (rowMap == 0)
        {
            return territoryMap == 0 ? [] : [territoryMap];
        }

        return territoryMap == 0 || territoryMap == rowMap ? [rowMap] : [rowMap, territoryMap];
    }
}
