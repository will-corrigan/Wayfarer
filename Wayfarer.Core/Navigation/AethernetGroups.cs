namespace Wayfarer.Core.Navigation;

/// <summary>A raw Aetheryte-sheet fact: one row's home territory and aethernet network
/// group (0 = not on any network). Deliberately position-free — see
/// <see cref="AethernetGroups.ForTerritory"/> for why group derivation must never ride
/// on position-resolved point lists.</summary>
public sealed record AethernetSheetRow(uint Territory, uint Group);

/// <summary>Derives "which aethernet networks can this territory reach for free?" from
/// raw sheet rows. This exists because deriving the same answer from
/// <see cref="AetherytePoint"/> lists is a live-proven trap: the plugin's point builder
/// drops any row whose position cannot be resolved, and in live sheet data EVERY
/// Ishgard shard row (83–87 for The Pillars, 80–82 for Foundation) carries Map=0 and
/// unresolvable Level refs — so the position-filtered list for The Pillars is empty,
/// its group set came out empty, and RouteCosting.TeleportCandidate's same-network
/// suppression silently never fired (third live "Teleport to Foundation first"
/// reproduction, 2026-08-22). Group membership is a pure sheet fact; positions are
/// irrelevant to it.</summary>
public static class AethernetGroups
{
    /// <summary>Every nonzero aethernet group present among <paramref name="rows"/>
    /// homed in <paramref name="territory"/>.</summary>
    public static HashSet<uint> ForTerritory(IEnumerable<AethernetSheetRow> rows, uint territory)
    {
        var groups = new HashSet<uint>();
        foreach (var row in rows)
        {
            if (row.Territory == territory && row.Group != 0)
            {
                groups.Add(row.Group);
            }
        }

        return groups;
    }
}
