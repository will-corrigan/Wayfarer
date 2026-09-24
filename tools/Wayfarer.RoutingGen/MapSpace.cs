using Lumina.Excel.Sheets;
using Lumina.Excel;
using Lumina;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>What the sheets say about maps: which territory's coordinate space a map's markers
/// are in, and how a marker's pixel position becomes a world position.
///
/// <para>Several TerritoryType rows can share one map. The one that owns aetheryte rows is the
/// one the player actually stands in, so it wins; otherwise the first row with that map.</para></summary>
internal sealed class MapSpace
{
    /// <summary>The pixel at the map's centre, which is world (0, 0) before the offset.</summary>
    private const float CentrePixel = 1024f;

    /// <summary>The map's scale is stored as a percentage.</summary>
    private const float ScalePercent = 100f;

    private readonly ExcelSheet<Map> maps;
    private readonly Dictionary<uint, uint> territoryByMap = [];
    private readonly Dictionary<uint, uint> mapByTerritory = [];

    public MapSpace(GameData game)
    {
        maps = game.Excel.GetSheet<Map>();

        var homed = new HashSet<uint>();
        foreach (var aetheryte in game.Excel.GetSheet<Aetheryte>())
        {
            homed.Add(aetheryte.Territory.RowId);
        }

        foreach (var territory in game.Excel.GetSheet<TerritoryType>())
        {
            var map = territory.Map.RowId;
            if (map == 0)
            {
                continue;
            }

            mapByTerritory[territory.RowId] = map;

            if (homed.Contains(territory.RowId) || !territoryByMap.ContainsKey(map))
            {
                territoryByMap[map] = territory.RowId;
            }
        }
    }

    /// <summary>The territory a map's markers are placed in: the territory that names the map as
    /// its own, else the territory the map row itself names (a city's sub-maps do this), else 0.</summary>
    public uint TerritoryOf(uint mapId) =>
        territoryByMap.TryGetValue(mapId, out var territory) ? territory : Row(mapId)?.TerritoryType.RowId ?? 0;

    /// <summary>A territory's own map, the one it names, or 0.</summary>
    public uint MapOf(uint territoryId) => mapByTerritory.GetValueOrDefault(territoryId);

    /// <summary>The map row, or null when there is none.</summary>
    public Map? Row(uint mapId) => maps.GetRowOrDefault(mapId);

    /// <summary>A marker's place in the world. Markers carry no height, so Y is unknown until the
    /// layouts say: a zero would be taken for a real floor.</summary>
    public Place Place(Map map, MapMarker marker)
    {
        var scale = map.SizeFactor / ScalePercent;
        var x = ((marker.X - CentrePixel) / scale) - map.OffsetX;
        var z = ((marker.Y - CentrePixel) / scale) - map.OffsetY;
        return new Place(TerritoryOf(map.RowId), map.RowId, x, float.NaN, z);
    }
}
