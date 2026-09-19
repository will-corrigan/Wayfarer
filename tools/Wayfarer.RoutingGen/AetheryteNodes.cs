using Lumina.Excel.Sheets;
using Lumina;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Every aetheryte and aethernet shard as a route node.
///
/// <para>A row's position comes from its Level reference when it has one, height included.
/// Otherwise its map marker stands in: an aetheryte's marker is keyed by the aetheryte row, a
/// shard's by its aethernet name, and it may sit on any map of the city, not the one the row
/// names. A shard with no marker at all, the airship landings, takes the place-name label the
/// city map draws for it.</para></summary>
internal static class AetheryteNodes
{
    private const byte PlaceNameMarker = 0;
    private const byte AetheryteMarker = 3;
    private const byte ShardMarker = 4;

    public static List<RouteNode> Read(GameData game, MapSpace maps)
    {
        var placed = new Dictionary<(byte Type, uint Key), Place>();
        var labelled = new Dictionary<uint, List<Place>>();
        IndexMarkers(game, maps, placed, labelled);

        var nodes = new List<RouteNode>();
        foreach (var row in game.Excel.GetSheet<Aetheryte>())
        {
            var isShard = !row.IsAetheryte && row.AethernetGroup != 0;
            if (!row.IsAetheryte && !isShard)
            {
                continue;
            }

            var name = (isShard ? row.AethernetName.ValueNullable?.Name : row.PlaceName.ValueNullable?.Name)?.ExtractText();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (Position(row, isShard, placed, labelled) is not { } at)
            {
                Console.Error.WriteLine($"no position for {(isShard ? "shard" : "aetheryte")} {row.RowId} {name}; skipped");
                continue;
            }

            nodes.Add(new RouteNode(row.RowId, name, isShard ? RouteNodeKind.Shard : RouteNodeKind.Aetheryte, row.AethernetGroup, at));
        }

        return nodes;
    }

    private static void IndexMarkers(GameData game, MapSpace maps, Dictionary<(byte Type, uint Key), Place> placed, Dictionary<uint, List<Place>> labelled)
    {
        var markers = game.Excel.GetSubrowSheet<MapMarker>();
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
                    case AetheryteMarker or ShardMarker when marker.DataKey.RowId != 0:
                        placed.TryAdd((marker.DataType, marker.DataKey.RowId), maps.Place(map, marker));
                        break;
                    case PlaceNameMarker when marker.PlaceNameSubtext.RowId != 0:
                        labelled.TryGetValue(marker.PlaceNameSubtext.RowId, out var places);
                        (places ?? (labelled[marker.PlaceNameSubtext.RowId] = [])).Add(maps.Place(map, marker));
                        break;
                }
            }
        }
    }

    private static Place? Position(Aetheryte row, bool isShard, Dictionary<(byte Type, uint Key), Place> placed, Dictionary<uint, List<Place>> labelled)
    {
        foreach (var reference in row.Level)
        {
            if (reference.RowId != 0 && reference.ValueNullable is { } level)
            {
                return new Place(level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z);
            }
        }

        var key = isShard ? (ShardMarker, row.AethernetName.RowId) : (AetheryteMarker, row.RowId);
        if (placed.TryGetValue(key, out var marked))
        {
            return OwnedBy(row, marked);
        }

        if (isShard && labelled.TryGetValue(row.AethernetName.RowId, out var labels))
        {
            var territoryMap = row.Territory.ValueNullable?.Map.RowId ?? 0;
            var label = labels.FirstOrDefault(place => place.Map == territoryMap) ?? labels[0];
            return OwnedBy(row, label);
        }

        return null;
    }

    /// <summary>The row knows its own territory better than the map does.</summary>
    private static Place OwnedBy(Aetheryte row, Place place) =>
        row.Territory.RowId == 0 ? place : place with { Territory = row.Territory.RowId };
}
