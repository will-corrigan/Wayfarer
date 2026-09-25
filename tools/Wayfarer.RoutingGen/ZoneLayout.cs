using System.Numerics;
using Lumina;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace Wayfarer.RoutingGen;

/// <summary>What one zone's layout files place, as much of it as routing needs: the spots warps
/// and aetherytes drop you on, the exits to other zones, the people who offer a warp, and the
/// boxes that say which of the zone's maps a point is on. Everything here has a real height, which
/// map markers do not: a marker is only a spot on the map's picture.
///
/// <para>A zone's layouts hold tens of thousands of other things, and every routable zone is read,
/// so nothing else is kept.</para></summary>
internal sealed class ZoneLayout
{
    /// <summary>The layout files the things routing needs are placed in.</summary>
    private static readonly string[] Files = ["planevent", "planmap", "planlive", "planner", "bg"];

    /// <summary>The developers' label for the layers the game's benchmark stages its scenes on.</summary>
    private const string BenchmarkLayer = "LVD_benchmark";

    private readonly List<MapRange> ranges = [];

    /// <summary>Everything that sends you somewhere: people who offer a warp, and doors and exits
    /// that are objects with a warp behind them. Person is zero for an object.</summary>
    public List<(uint Warp, uint Person, Vector3 At, ushort Festival, ushort Phase)> Warpers { get; } = [];

    /// <summary>Spots something drops you on, by instance: warp landings, aetherytes, shards.</summary>
    public Dictionary<uint, Vector3> Spots { get; } = [];

    /// <summary>Where the zone's exits are, which zone each leads to, the spot in that zone it
    /// lands on, and the spot in this zone that coming back lands on: pop ranges, by instance, or
    /// zero when the exit names none. An exit is a box whose middle can be well above the ground; the
    /// spot coming back lands on is on the ground beside it.</summary>
    public List<(Trigger Box, uint Leads, uint Lands, uint Returns)> Exits { get; } = [];

    /// <summary>The aetherytes and shards the zone places, by their row: what can be boarded here.
    /// The aethernet also drops you at spots outside a city's gate with nothing there to board.</summary>
    public HashSet<uint> Aetherytes { get; } = [];

    /// <summary>Where things stand on the zone's floors: people, objects and landing spots. Each
    /// says how high the floor is where it stands, which nothing on a map does.</summary>
    public List<Vector3> Standing { get; } = [];

    /// <summary>Reads a zone's layouts. A file Lumina cannot read is named and left out.</summary>
    /// <param name="game">The game's files.</param>
    /// <param name="territory">The zone, or null for none.</param>
    /// <param name="warpsOf">The warps a person offers, by who they are.</param>
    /// <param name="objectWarp">The warp an object sends you through, or zero.</param>
    public static ZoneLayout Read(LayoutFiles game, TerritoryType? territory, Func<uint, IEnumerable<uint>> warpsOf, Func<uint, uint> objectWarp)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(warpsOf);
        ArgumentNullException.ThrowIfNull(objectWarp);

        var layout = new ZoneLayout();
        var bg = territory?.Bg.ExtractText() ?? string.Empty;
        if (bg.Length == 0)
        {
            return layout;
        }

        var folder = "bg/" + bg[..bg.LastIndexOf('/')];
        foreach (var file in Files)
        {
            LgbFile? lgb;
            try
            {
                lgb = game.Get($"{folder}/{file}.lgb");
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or NotSupportedException or OutOfMemoryException or IndexOutOfRangeException or EndOfStreamException)
            {
                // Lumina cannot read every layout. Twelve A Realm Reborn planner.lgb files carry a
                // longer header and layer group than it expects, so it reads a layer count from the
                // wrong bytes and asks for an impossible amount of memory. Seven of them hold no
                // layers; the other five hold only level-design layers (positions, a navmesh, the
                // benchmark's stand-ins) with no warp landing and no warp-giving person, so leaving
                // them out loses nothing the graph uses. Such a file is named, not the run lost. A
                // mirror failure is not caught here: it stops the run, as LayoutFiles explains.
                Console.Error.WriteLine($"  {folder}/{file}.lgb could not be read: {ex.GetType().Name}");
                continue;
            }

            if (lgb is null)
            {
                continue;
            }

            foreach (var layer in lgb.Layers)
            {
                // The benchmark's staging stands people where they never stand in play: four
                // copies of Gridania's innkeeper in the Central Shroud, offering his inn room. The
                // layer is known only by the developers' own label for it; which layers are live
                // is decided by the server, and the client's files do not say.
                if (layer.Name.StartsWith(BenchmarkLayer, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var thing in layer.InstanceObjects)
                {
                    layout.Take(thing, layer.FestivalID, layer.FestivalPhaseID, warpsOf, objectWarp);
                }
            }
        }

        return layout;
    }

    /// <summary>The floor height of a map at a spot on the ground: the height of whatever stands
    /// nearest that spot inside the map's own range, or null when nothing stands within reach.
    /// A range says only between which heights a map lies; what stands in it says where its floor
    /// is.</summary>
    public float? FloorOf(uint map, Vector2 ground, float reach)
    {
        // A zone of one map draws no ranges at all: everything standing in it is on that map.
        var mine = ranges.Where(range => range.Map == map).ToList();
        var onlyMap = ranges.Count == 0;
        if (mine.Count == 0 && !onlyMap)
        {
            return null;
        }

        float? floor = null;
        var nearest = reach;
        foreach (var at in Standing)
        {
            var far = Vector2.Distance(ground, new Vector2(at.X, at.Z));
            if (far <= nearest && (onlyMap || (mine.Exists(range => range.Holds(at)) && MapAt(at) == map)))
            {
                (nearest, floor) = (far, at.Y);
            }
        }

        return floor;
    }

    /// <summary>How high the ground is beside an exit, or null when nothing says: where coming back
    /// through it lands, when that is within reach across the ground, or else the floor of the map
    /// the exit is on. Never the exit's own height, which is its box's middle.</summary>
    public float? GroundBeside(Vector3 box, uint returns, uint map, float reach)
    {
        var ground = new Vector2(box.X, box.Z);
        return returns != 0 && Spots.TryGetValue(returns, out var back) && Vector2.Distance(ground, new Vector2(back.X, back.Z)) <= reach
            ? back.Y
            : FloorOf(map, ground, reach);
    }

    /// <summary>The map a point is on: the highest-priority range it stands in, or null when it
    /// stands in none.</summary>
    public uint? MapAt(Vector3 at) =>
        ranges.Where(range => range.Holds(at)).OrderByDescending(range => range.Priority).Select(range => (uint?)range.Map).FirstOrDefault();

    /// <summary>Keeps what routing needs of one thing a layer places.
    ///
    /// <para>A seasonal event's layer, such as the Moonfire Faire's, is only there while the event
    /// runs. Its warps are kept with the event's id, and a route takes them only while the game
    /// says that event is on; its landing spots are kept, since only its own warps land on them.
    /// Nothing else of it is kept: an exit, a map's range or a floor that exists only some weeks of
    /// the year has no way to be switched off.</para></summary>
    private void Take(LayerCommon.InstanceObject thing, ushort festival, ushort phase, Func<uint, IEnumerable<uint>> warpsOf, Func<uint, uint> objectWarp)
    {
        var at = new Vector3(thing.Transform.Translation.X, thing.Transform.Translation.Y, thing.Transform.Translation.Z);
        if (festival != 0 && thing.Object is not (LayerCommon.PopRangeInstanceObject or LayerCommon.ENPCInstanceObject or LayerCommon.EventInstanceObject))
        {
            return;
        }

        if (festival == 0 && thing.Object is LayerCommon.PopRangeInstanceObject or LayerCommon.ENPCInstanceObject or LayerCommon.EventInstanceObject)
        {
            Standing.Add(at);
        }

        switch (thing.Object)
        {
            case LayerCommon.PopRangeInstanceObject:
                Spots.TryAdd(thing.InstanceId, at);
                break;
            case LayerCommon.ExitRangeInstanceObject exit when exit.TerritoryType != 0:
                Exits.Add((Trigger.Of(exit.ParentData.TriggerBoxShape, thing), exit.TerritoryType, exit.DestInstanceId, exit.ReturnInstanceId));
                break;
            case LayerCommon.ENPCInstanceObject person when person.ParentData.ParentData.BaseId != 0:
                foreach (var warp in warpsOf(person.ParentData.ParentData.BaseId))
                {
                    Warpers.Add((warp, person.ParentData.ParentData.BaseId, at, festival, phase));
                }

                break;
            case LayerCommon.EventInstanceObject door when objectWarp(door.ParentData.BaseId) is var warp and not 0:
                Warpers.Add((warp, 0u, at, festival, phase));
                break;
            case LayerCommon.MapRangeInstanceObject range when range.Map != 0:
                ranges.Add(new MapRange(range.Map, range.ParentData.Priority, Trigger.Of(range.ParentData.TriggerBoxShape, thing)));
                break;
            case LayerCommon.AetheryteInstanceObject aetheryte:
                Aetherytes.Add(aetheryte.ParentData.BaseId);
                break;
        }
    }

    /// <summary>A trigger of a zone that says which map a point in it is on.</summary>
    private sealed record MapRange(uint Map, short Priority, Trigger Box)
    {
        public bool Holds(Vector3 at) => Box.Holds(at);
    }
}
