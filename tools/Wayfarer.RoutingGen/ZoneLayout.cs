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

    private readonly List<MapRange> ranges = [];

    /// <summary>Everything that sends you somewhere: people who offer a warp, and doors and exits
    /// that are objects with a warp behind them. Person is zero for an object.</summary>
    public List<(uint Warp, uint Person, Vector3 At)> Warpers { get; } = [];

    /// <summary>Spots something drops you on, by instance: warp landings, aetherytes, shards.</summary>
    public Dictionary<uint, Vector3> Spots { get; } = [];

    /// <summary>Where the zone's exits are, and which zone each leads to.</summary>
    public List<(Vector3 At, uint Leads)> Exits { get; } = [];

    /// <summary>Where things stand on the zone's floors: people, objects and landing spots. Each
    /// says how high the floor is where it stands, which nothing on a map does.</summary>
    public List<Vector3> Standing { get; } = [];

    /// <summary>Reads a zone's layouts. A file Lumina cannot read is named and left out.</summary>
    /// <param name="game">The game's files.</param>
    /// <param name="territory">The zone, or null for none.</param>
    /// <param name="warpsOf">The warps a person offers, by who they are.</param>
    /// <param name="objectWarp">The warp an object sends you through, or zero.</param>
    public static ZoneLayout Read(GameData game, TerritoryType? territory, Func<uint, IEnumerable<uint>> warpsOf, Func<uint, uint> objectWarp)
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
                lgb = game.GetFile<LgbFile>($"{folder}/{file}.lgb");
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or NotSupportedException or OutOfMemoryException or IndexOutOfRangeException or EndOfStreamException)
            {
                // Lumina cannot read every layout: a few ask for an impossible amount of memory
                // part-way through. Such a file is left out and named, not the whole run lost.
                Console.Error.WriteLine($"  {folder}/{file}.lgb could not be read: {ex.GetType().Name}");
                continue;
            }

            if (lgb is null)
            {
                continue;
            }

            foreach (var layer in lgb.Layers)
            {
                foreach (var thing in layer.InstanceObjects)
                {
                    layout.Take(thing, warpsOf, objectWarp);
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

    /// <summary>The map a point is on: the highest-priority range it stands in, or null when it
    /// stands in none.</summary>
    public uint? MapAt(Vector3 at) =>
        ranges.Where(range => range.Holds(at)).OrderByDescending(range => range.Priority).Select(range => (uint?)range.Map).FirstOrDefault();

    private void Take(LayerCommon.InstanceObject thing, Func<uint, IEnumerable<uint>> warpsOf, Func<uint, uint> objectWarp)
    {
        var at = new Vector3(thing.Transform.Translation.X, thing.Transform.Translation.Y, thing.Transform.Translation.Z);
        if (thing.Object is LayerCommon.PopRangeInstanceObject or LayerCommon.ENPCInstanceObject or LayerCommon.EventInstanceObject)
        {
            Standing.Add(at);
        }

        switch (thing.Object)
        {
            case LayerCommon.PopRangeInstanceObject:
                Spots.TryAdd(thing.InstanceId, at);
                break;
            case LayerCommon.ExitRangeInstanceObject exit when exit.TerritoryType != 0:
                Exits.Add((at, exit.TerritoryType));
                break;
            case LayerCommon.ENPCInstanceObject person when person.ParentData.ParentData.BaseId != 0:
                foreach (var warp in warpsOf(person.ParentData.ParentData.BaseId))
                {
                    Warpers.Add((warp, person.ParentData.ParentData.BaseId, at));
                }

                break;
            case LayerCommon.EventInstanceObject door when objectWarp(door.ParentData.BaseId) is var warp and not 0:
                Warpers.Add((warp, 0u, at));
                break;
            case LayerCommon.MapRangeInstanceObject range when range.Map != 0:
                ranges.Add(new MapRange(range.Map, range.ParentData.TriggerBoxShape, range.ParentData.Priority, at, thing.Transform.Rotation.Y, new Vector3(thing.Transform.Scale.X, thing.Transform.Scale.Y, thing.Transform.Scale.Z)));
                break;
        }
    }

    /// <summary>A box, cylinder or sphere of a zone that says which map a point in it is on. Its
    /// scale is half its size on each axis.</summary>
    private sealed record MapRange(uint Map, TriggerBoxShape Shape, short Priority, Vector3 Centre, float Turn, Vector3 Half)
    {
        public bool Holds(Vector3 at)
        {
            var offset = at - Centre;
            switch (Shape)
            {
                case TriggerBoxShape.TriggerBoxShapeCylinder:
                    return MathF.Abs(offset.Y) <= Half.Y && ((offset.X * offset.X) + (offset.Z * offset.Z)) <= Half.X * Half.X;
                case TriggerBoxShape.TriggerBoxShapeSphere:
                    return offset.Length() <= Half.X;
                default:
                    // Turned back by the box's own turn, so its sides line up with the axes.
                    var (sin, cos) = MathF.SinCos(-Turn);
                    var x = (offset.X * cos) - (offset.Z * sin);
                    var z = (offset.X * sin) + (offset.Z * cos);
                    return MathF.Abs(x) <= Half.X && MathF.Abs(offset.Y) <= Half.Y && MathF.Abs(z) <= Half.Z;
            }
        }
    }
}
