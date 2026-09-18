using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Every aetheryte and aethernet shard as a route node.
///
/// <para>A row's position comes from its Level reference when it has one, height included.
/// When it has none, the row's map marker stands in: an aetheryte's marker is keyed by the
/// aetheryte row, a shard's by its aethernet name. The row's own Map reference is not reliable —
/// every Ishgard shard row carries Map 0 while the territory's map carries the markers — so the
/// territory's map is searched too.</para></summary>
internal static class AetheryteNodes
{
    private const byte AetheryteMarker = 3;
    private const byte ShardMarker = 4;

    public static List<RouteNode> Read(GameData game, MapSpace maps)
    {
        var markers = game.Excel.GetSubrowSheet<MapMarker>();
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

            if (Position(row, maps, markers) is not { } at)
            {
                Console.Error.WriteLine($"no position for {(isShard ? "shard" : "aetheryte")} {row.RowId} {name}; skipped");
                continue;
            }

            nodes.Add(new RouteNode(row.RowId, name, isShard ? RouteNodeKind.Shard : RouteNodeKind.Aetheryte, row.AethernetGroup, at));
        }

        return nodes;
    }

    private static Place? Position(Aetheryte row, MapSpace maps, SubrowExcelSheet<MapMarker> markers)
    {
        foreach (var reference in row.Level)
        {
            if (reference.RowId != 0 && reference.ValueNullable is { } level)
            {
                return new Place(level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z);
            }
        }

        var territoryMap = row.Territory.ValueNullable?.Map.RowId ?? 0;
        foreach (var mapId in new[] { row.Map.RowId, territoryMap }.Where(id => id != 0).Distinct())
        {
            if (maps.Row(mapId) is not { } map || !markers.HasRow(map.MapMarkerRange))
            {
                continue;
            }

            foreach (var marker in markers[map.MapMarkerRange])
            {
                var matches = marker.DataType switch
                {
                    AetheryteMarker => marker.DataKey.RowId == row.RowId,
                    ShardMarker => marker.DataKey.RowId == row.AethernetName.RowId,
                    _ => false,
                };

                if (matches)
                {
                    // The row knows its own territory better than the map does.
                    var place = maps.Place(map, marker);
                    return row.Territory.RowId == 0 ? place : place with { Territory = row.Territory.RowId };
                }
            }
        }

        return null;
    }
}
