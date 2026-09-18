using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Every door between two maps, from the map-link markers each map draws: a marker of
/// type 1 leads to an adjacent map, type 2 into an interior, and its key is the map it leads to.
///
/// <para>A door has two sides. The far side is the marker on the destination map that leads back,
/// paired by label when several do, and by order otherwise. A destination with no marker back —
/// an interior that draws no markers at all — gets a far side at the near side's own position,
/// which is the only position the data gives for it.</para></summary>
internal static class DoorLinks
{
    private const byte AdjacentMapMarker = 1;
    private const byte InteriorMapMarker = 2;
    private const string UnnamedDoor = "entrance";

    public static List<DoorLink> Read(GameData game, MapSpace maps)
    {
        var markers = game.Excel.GetSubrowSheet<MapMarker>();
        var links = new Dictionary<(uint From, uint To), List<(string Name, Place At)>>();

        foreach (var map in game.Excel.GetSheet<Map>())
        {
            if (map.MapMarkerRange == 0 || !markers.HasRow(map.MapMarkerRange) || maps.TerritoryOf(map.RowId) == 0)
            {
                continue;
            }

            foreach (var marker in markers[map.MapMarkerRange])
            {
                if (marker.DataType is not (AdjacentMapMarker or InteriorMapMarker) || marker.DataKey.RowId == 0 || marker.DataKey.RowId == map.RowId)
                {
                    continue;
                }

                var name = marker.PlaceNameSubtext.ValueNullable?.Name.ExtractText();
                if (string.IsNullOrEmpty(name))
                {
                    name = maps.Row(marker.DataKey.RowId)?.PlaceName.ValueNullable?.Name.ExtractText();
                }

                var key = (map.RowId, marker.DataKey.RowId);
                if (!links.TryGetValue(key, out var sides))
                {
                    links[key] = sides = [];
                }

                sides.Add((string.IsNullOrEmpty(name) ? UnnamedDoor : name, maps.Place(map, marker)));
            }
        }

        return Pair(links, maps);
    }

    /// <summary>Joins each near side with a far side. Each unordered pair of maps is visited
    /// once, from the lower map id, so a two-way door is one link.</summary>
    private static List<DoorLink> Pair(Dictionary<(uint From, uint To), List<(string Name, Place At)>> links, MapSpace maps)
    {
        var doors = new List<DoorLink>();
        foreach (var ((from, to), nearSides) in links)
        {
            if (to < from && links.ContainsKey((to, from)))
            {
                continue;
            }

            var farSides = links.GetValueOrDefault((to, from)) ?? [];
            var unpaired = new List<(string Name, Place At)>(farSides);

            for (var i = 0; i < nearSides.Count; i++)
            {
                var (name, near) = nearSides[i];
                var farIndex = unpaired.FindIndex(f => string.Equals(f.Name, name, StringComparison.Ordinal));
                if (farIndex < 0 && unpaired.Count > 0)
                {
                    farIndex = 0;
                }

                Place far;
                if (farIndex >= 0)
                {
                    far = unpaired[farIndex].At;
                    unpaired.RemoveAt(farIndex);
                }
                else
                {
                    far = near with { Territory = maps.TerritoryOf(to), Map = to };
                }

                doors.Add(new DoorLink(name, near, far));
            }
        }

        return doors;
    }
}
