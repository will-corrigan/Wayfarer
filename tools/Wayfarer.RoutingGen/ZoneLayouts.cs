using Lumina;
using Lumina.Excel.Sheets;

namespace Wayfarer.RoutingGen;

/// <summary>Every zone's layout, read once each when first asked for.</summary>
internal sealed class ZoneLayouts
{
    private readonly GameData game;
    private readonly Lumina.Excel.ExcelSheet<TerritoryType> territories;
    private readonly Lumina.Excel.ExcelSheet<ENpcBase> people;
    private readonly Lumina.Excel.ExcelSheet<EObj> objects;
    private readonly Lumina.Excel.ExcelSheet<Warp> warps;
    private readonly Dictionary<uint, ZoneLayout> read = [];

    public ZoneLayouts(GameData game)
    {
        this.game = game;
        territories = game.Excel.GetSheet<TerritoryType>();
        people = game.Excel.GetSheet<ENpcBase>();
        objects = game.Excel.GetSheet<EObj>();
        warps = game.Excel.GetSheet<Warp>();
    }

    /// <summary>How many zones have been read.</summary>
    public int Count => read.Count;

    /// <summary>One zone's layout.</summary>
    public ZoneLayout Of(uint territory)
    {
        if (!read.TryGetValue(territory, out var layout))
        {
            read[territory] = layout = ZoneLayout.Read(game, territories.GetRowOrDefault(territory), WarpsOf, ObjectWarp);
        }

        return layout;
    }

    /// <summary>The warps a person offers: whichever of their event data are warp rows.</summary>
    private IEnumerable<uint> WarpsOf(uint person) =>
        people.GetRowOrDefault(person) is { } npc
            ? npc.ENpcData.Select(data => data.RowId).Where(id => id != 0 && warps.HasRow(id)).ToList()
            : [];

    /// <summary>The warp an object sends you through, or zero: a door, an exit, a lift's lever.</summary>
    private uint ObjectWarp(uint thing) =>
        objects.GetRowOrDefault(thing) is { } row && row.Data.RowId != 0 && warps.HasRow(row.Data.RowId) ? row.Data.RowId : 0u;
}
