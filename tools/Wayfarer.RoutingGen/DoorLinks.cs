using Lumina.Excel.Sheets;
using Lumina.Excel;
using Lumina;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Every door between two maps, from two kinds of marker the maps draw.
///
/// <para>A map-link marker (type 1 to an adjacent map, type 2 into an interior) is keyed by the
/// map it leads to. Its far side is the marker on the destination map that leads back, paired by
/// label when several do and by order otherwise, or the near side's own position mirrored when the
/// destination draws no marker back.</para>
///
/// <para>Some interiors draw no markers of their own and no map links into them exist either: the
/// only thing that names them is a place-name label on the enclosing map, at the door. Fortemps
/// Manor is one. Such a label becomes a door into the interior, far side mirrored.</para></summary>
internal static class DoorLinks
{
    private const byte PlaceNameMarker = 0;
    private const byte AdjacentMapMarker = 1;
    private const byte InteriorMapMarker = 2;
    private const string UnnamedDoor = "entrance";

    public static List<DoorLink> Read(GameData game, MapSpace maps)
    {
        var markers = game.Excel.GetSubrowSheet<MapMarker>();
        var links = new Dictionary<(uint From, uint To), List<(string Name, Place At)>>();
        var labels = new Dictionary<uint, List<(string Name, Place At)>>();

        foreach (var map in game.Excel.GetSheet<Map>())
        {
            if (map.MapMarkerRange == 0 || !markers.HasRow(map.MapMarkerRange) || maps.TerritoryOf(map.RowId) == 0)
            {
                continue;
            }

            foreach (var marker in markers[map.MapMarkerRange])
            {
                switch (marker.DataType)
                {
                    case AdjacentMapMarker or InteriorMapMarker when marker.DataKey.RowId != 0 && marker.DataKey.RowId != map.RowId:
                        Add(links, (map.RowId, marker.DataKey.RowId), LinkName(marker, maps.Row(marker.DataKey.RowId)), maps.Place(map, marker));
                        break;
                    case PlaceNameMarker when marker.PlaceNameSubtext.RowId != 0:
                        Add(labels, marker.PlaceNameSubtext.RowId, marker.PlaceNameSubtext.Value.Name.ExtractText(), maps.Place(map, marker));
                        break;
                }
            }
        }

        var doors = Pair(links, maps);
        doors.AddRange(UnmarkedInteriors(game, maps, markers, labels));
        return doors;
    }

    private static void Add<TKey>(Dictionary<TKey, List<(string Name, Place At)>> into, TKey key, string name, Place at)
        where TKey : notnull
    {
        if (!into.TryGetValue(key, out var sides))
        {
            into[key] = sides = [];
        }

        sides.Add((name, at));
    }

    private static string LinkName(MapMarker marker, Map? destination) =>
        marker.PlaceNameSubtext.ValueNullable?.Name.ExtractText() is { Length: > 0 } own
            ? own
            : destination?.PlaceName.ValueNullable?.Name.ExtractText() is { Length: > 0 } theirs ? theirs : UnnamedDoor;

    /// <summary>Joins each near side with a far side. Each unordered pair of maps is visited once,
    /// from the lower map id, so a two-way door is one link.</summary>
    private static List<DoorLink> Pair(Dictionary<(uint From, uint To), List<(string Name, Place At)>> links, MapSpace maps)
    {
        var doors = new List<DoorLink>();
        foreach (var ((from, to), nearSides) in links)
        {
            if (to < from && links.ContainsKey((to, from)))
            {
                continue;
            }

            var unpaired = new List<(string Name, Place At)>(links.GetValueOrDefault((to, from)) ?? []);
            foreach (var (name, near) in nearSides)
            {
                var farIndex = unpaired.FindIndex(f => string.Equals(f.Name, name, StringComparison.Ordinal));
                if (farIndex < 0 && unpaired.Count > 0)
                {
                    farIndex = 0;
                }

                var far = farIndex >= 0 ? unpaired[farIndex].At : near with { Territory = maps.TerritoryOf(to), Map = to };
                if (farIndex >= 0)
                {
                    unpaired.RemoveAt(farIndex);
                }

                doors.Add(new DoorLink(name, near, far));
            }
        }

        return doors;
    }

    /// <summary>A door into every territory that draws no markers, has no map link into it, and is
    /// named by a place-name label on some other map: the label is the door.</summary>
    private static IEnumerable<DoorLink> UnmarkedInteriors(
        GameData game,
        MapSpace maps,
        SubrowExcelSheet<MapMarker> markers,
        Dictionary<uint, List<(string Name, Place At)>> labels)
    {
        var homed = game.Excel.GetSheet<Aetheryte>().Select(a => a.Territory.RowId).ToHashSet();
        foreach (var territory in game.Excel.GetSheet<TerritoryType>())
        {
            var map = territory.Map.RowId == 0 ? null : maps.Row(territory.Map.RowId);
            var drawsMarkers = map is { MapMarkerRange: not 0 } && markers.HasRow(map.Value.MapMarkerRange);
            if (map is null || drawsMarkers || homed.Contains(territory.RowId) || !labels.TryGetValue(territory.PlaceName.RowId, out var doorsIn))
            {
                continue;
            }

            foreach (var (name, near) in doorsIn.Where(label => label.At.Map != territory.Map.RowId))
            {
                yield return new DoorLink(name, near, near with { Territory = territory.RowId, Map = territory.Map.RowId });
            }
        }
    }
}
