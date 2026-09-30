using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Treasure;

/// <summary>Every place a zone's layout puts treasure, read from the zone's own files in the
/// player's install the first time the zone is asked about.
///
/// <para>No sheet lists them. A zone's <c>planmap.lgb</c> places one treasure object per spot: a
/// dungeon's chests, the Occult Crescent's coffer spawn points, Bozja's and Eureka's. The server
/// decides which are filled. A zone of several maps says which map a point is on with map ranges,
/// the same boxes the routing generator reads; a spot inside none is on the zone's own map. Layers
/// that exist only during a seasonal event are left out.</para></summary>
internal sealed class TreasureSpots(IDataManager data, IPluginLog log)
{
    /// <summary>The one layout file whose treasure is real. A scenery file places a few chests too, in
    /// Middle La Noscea among others, and those are set dressing nobody can open.</summary>
    private const string TreasureFile = "planmap";

    /// <summary>The layout files map ranges are read from: the small ones only. Never <c>planner</c>
    /// or <c>bg</c> inside the game. Lumina misreads a dozen A Realm Reborn planner files, taking a
    /// garbage layer count and asking for an impossible amount of memory before it gives up, and a
    /// city's bg file is enormous. Reading Ul'dah's planner in the background once held the game's
    /// file lock for 27 seconds, froze the quest guidance waiting behind it on the game's thread,
    /// and was followed by the game crashing. The routing generator reads them offline, where that
    /// costs nothing.</summary>
    private static readonly string[] LayoutFiles = ["planmap", "planevent", "planlive"];

    private readonly ConcurrentDictionary<uint, Lazy<IReadOnlyList<TreasureSpot>>> read = new();

    /// <summary>The treasure spots of a zone, reading its layout the first time. Off the game's
    /// thread: a layout is a few hundred kilobytes.</summary>
    public IReadOnlyList<TreasureSpot> In(uint territory) =>
        read.GetOrAdd(territory, zone => new Lazy<IReadOnlyList<TreasureSpot>>(() => Read(zone))).Value;

    /// <summary>The map a point is on: the highest-priority range it stands in, or null.</summary>
    private static uint? MapAt(Vector3 at, List<(uint Map, short Priority, Trigger Box)> ranges) =>
        ranges.Where(range => range.Box.Holds(at)).OrderByDescending(range => range.Priority).Select(range => (uint?)range.Map).FirstOrDefault();

    /// <summary>One layout file, or null when the zone has none or it cannot be read. Lumina cannot
    /// read every layout (a dozen A Realm Reborn planner files among them), and one unreadable file
    /// must not cost the zone the spots in the others.</summary>
    private LgbFile? Layout(string path)
    {
        try
        {
            return data.GetFile<LgbFile>(path);
        }
        catch (Exception ex)
        {
            log.Debug($"treasure: {path} could not be read ({ex.GetType().Name}), so it is left out.");
            return null;
        }
    }

    private List<TreasureSpot> Read(uint territory)
    {
        try
        {
            if (data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory) is not { } zone
                || zone.Bg.ExtractText() is not { Length: > 0 } bg
                || !bg.Contains('/', StringComparison.Ordinal))
            {
                return [];
            }

            var folder = $"bg/{bg[..bg.LastIndexOf('/')]}";
            var ranges = new List<(uint Map, short Priority, Trigger Box)>();
            var chests = new List<Vector3>();
            foreach (var file in LayoutFiles)
            {
                if (Layout($"{folder}/{file}.lgb") is not { } layout)
                {
                    continue;
                }

                foreach (var layer in layout.Layers.Where(layer => layer.FestivalID == 0))
                {
                    foreach (var thing in layer.InstanceObjects)
                    {
                        var at = new Vector3(thing.Transform.Translation.X, thing.Transform.Translation.Y, thing.Transform.Translation.Z);
                        switch (thing.Object)
                        {
                            case LayerCommon.MapRangeInstanceObject range when range.Map != 0:
                                ranges.Add((range.Map, range.ParentData.Priority, Trigger.Of(range.ParentData.TriggerBoxShape, thing)));
                                break;
                            case LayerCommon.TreasureInstanceObject when string.Equals(file, TreasureFile, StringComparison.Ordinal):
                                chests.Add(at);
                                break;
                        }
                    }
                }
            }

            var spots = chests
                .Select(at => new TreasureSpot(territory, MapAt(at, ranges) ?? zone.Map.RowId, at))
                .ToList();
            log.Debug($"treasure: {spots.Count} spots in territory {territory} across {spots.Select(spot => spot.Map).Distinct().Count()} maps.");
            return spots;
        }
        catch (Exception ex)
        {
            // Any failure, not only the expected ones: a patch can change a layout file in ways the
            // reader chokes on, and a zone whose spots are lost must not stop live treasure showing.
            log.Warning(ex, $"treasure: the layout of territory {territory} could not be read, so its treasure spots are not marked.");
            return [];
        }
    }
}
