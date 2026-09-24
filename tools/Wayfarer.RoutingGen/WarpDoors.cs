using System.Numerics;
using Lumina;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Every door that is passed by asking someone: a lift attendant, an airship's purser, a
/// ferry's skipper. The sheets name what each person offers; where they stand and where they send
/// you is in each zone's layout files.
///
/// <para>A person offers a warp through their event data. The warp names the zone it lands in and
/// the spot, a pop range placed in that zone's layout. Both ends are found by reading the layouts,
/// and each end's map is the map range box of that zone the point stands in, the same boxes the
/// game uses to say which map the player is on. A lift in Ul'dah lands on the airship landing's
/// own map, a floor above the street at the same place on the ground.</para>
///
/// <para>Warps that cost gil are kept: paying is not a gap. Warps kept until quests are done carry
/// those quests, and a route only takes them once the player has done them. Every warp is one way;
/// the way back is whoever stands at the other end.</para></summary>
internal static class WarpDoors
{
    /// <summary>The layout files a zone's people, landing spots and map ranges are placed in.</summary>
    private static readonly string[] LayoutFiles = ["planevent", "planmap", "planlive", "planner", "bg"];

    public static List<DoorLink> Read(GameData game, MapSpace maps, IReadOnlySet<uint> routable)
    {
        var territories = game.Excel.GetSheet<TerritoryType>();
        var people = game.Excel.GetSheet<ENpcBase>();
        var names = game.Excel.GetSheet<ENpcResident>();
        var warps = game.Excel.GetSheet<Warp>();
        var layouts = new Dictionary<uint, Layout>();

        bool OffersWarp(uint person) =>
            people.GetRowOrDefault(person) is { } npc && npc.ENpcData.Any(data => data.RowId != 0 && warps.HasRow(data.RowId));

        Layout LayoutOf(uint territory)
        {
            if (!layouts.TryGetValue(territory, out var layout))
            {
                layouts[territory] = layout = Layout.Read(game, territories.GetRowOrDefault(territory), OffersWarp);
            }

            return layout;
        }

        var doors = new List<DoorLink>();
        foreach (var territory in routable.Order())
        {
            var here = LayoutOf(territory);
            foreach (var (person, standing) in here.People)
            {
                if (people.GetRowOrDefault(person) is not { } npc)
                {
                    continue;
                }

                foreach (var data in npc.ENpcData)
                {
                    if (data.RowId == 0 || warps.GetRowOrDefault(data.RowId) is not { } warp)
                    {
                        continue;
                    }

                    var landsIn = warp.TerritoryType.RowId;
                    var name = warp.Name.ExtractText();
                    if (name.Length == 0 || !routable.Contains(landsIn) || !LayoutOf(landsIn).Spots.TryGetValue(warp.PopRange.RowId, out var landing))
                    {
                        continue;
                    }

                    doors.Add(new DoorLink(
                        name,
                        Place(here, territory, standing, maps),
                        Place(LayoutOf(landsIn), landsIn, landing, maps),
                        OneWay: true,
                        Npc: Shown(names.GetRowOrDefault(person)?.Singular.ExtractText() ?? string.Empty),
                        Quests: Quests(warp.WarpCondition.ValueNullable)));
                }
            }
        }

        Console.Error.WriteLine($"  read the layouts of {layouts.Count} zones for {doors.Count} doors taken by asking");
        return doors;
    }

    /// <summary>The quests a warp is kept until, or null for none.</summary>
    private static List<uint>? Quests(WarpCondition? condition)
    {
        if (condition is not { } kept)
        {
            return null;
        }

        List<uint> quests = [.. new[] { kept.RequiredQuest1, kept.RequiredQuest2, kept.RequiredQuest3, kept.RequiredQuest4 }
            .Select(quest => quest.RowId)
            .Where(quest => quest != 0)];
        return quests.Count == 0 ? null : quests;
    }

    /// <summary>A name as the game shows it: begun with a capital. The sheet writes a title like
    /// "arrivals attendant" in lower case and leaves the capital to the window.</summary>
    private static string Shown(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>A point in a zone as the graph holds it: on the map whose range it stands in, at the
    /// height the layout places it. Stops read off map markers have no height, since a marker is only
    /// a spot on the map's picture, but a layout knows how high a person stands.</summary>
    private static Place Place(Layout layout, uint territory, Vector3 at, MapSpace maps)
    {
        var map = layout.MapAt(at) ?? maps.MapOf(territory);
        return new Place(territory, map, at.X, at.Y, at.Z);
    }

    /// <summary>What one zone's layout files place: its people by who they are, its landing spots
    /// by instance, and the boxes that say which of its maps a point is on.</summary>
    private sealed class Layout
    {
        public List<(uint Person, Vector3 At)> People { get; } = [];

        public Dictionary<uint, Vector3> Spots { get; } = [];

        private List<MapRange> Ranges { get; } = [];

        public static Layout Read(GameData game, TerritoryType? territory, Func<uint, bool> offersWarp)
        {
            var layout = new Layout();
            var bg = territory?.Bg.ExtractText() ?? string.Empty;
            if (bg.Length == 0)
            {
                return layout;
            }

            var folder = "bg/" + bg[..bg.LastIndexOf('/')];
            foreach (var file in LayoutFiles)
            {
                LgbFile? lgb;
                try
                {
                    lgb = game.GetFile<LgbFile>($"{folder}/{file}.lgb");
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or NotSupportedException)
                {
                    Console.Error.WriteLine($"  {folder}/{file}.lgb could not be read: {ex.Message}");
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
                        // Only what a warp can need is kept: where people stand who offer one, the spots
                        // warps land on, and the boxes that say which map a point is on. A zone's
                        // layouts hold tens of thousands of other things, and every zone is read.
                        var at = new Vector3(thing.Transform.Translation.X, thing.Transform.Translation.Y, thing.Transform.Translation.Z);
                        switch (thing.Object)
                        {
                            case LayerCommon.PopRangeInstanceObject:
                                layout.Spots.TryAdd(thing.InstanceId, at);
                                break;
                            case LayerCommon.ENPCInstanceObject person when person.ParentData.ParentData.BaseId != 0 && offersWarp(person.ParentData.ParentData.BaseId):
                                layout.People.Add((person.ParentData.ParentData.BaseId, at));
                                break;
                            case LayerCommon.MapRangeInstanceObject range when range.Map != 0:
                                layout.Ranges.Add(new MapRange(range.Map, range.ParentData.TriggerBoxShape, range.ParentData.Priority, at, thing.Transform.Rotation.Y, new Vector3(thing.Transform.Scale.X, thing.Transform.Scale.Y, thing.Transform.Scale.Z)));
                                break;
                        }
                    }
                }
            }

            return layout;
        }

        /// <summary>The map a point is on: the highest-priority range it stands in, or null when it
        /// stands in none.</summary>
        public uint? MapAt(Vector3 at) =>
            Ranges.Where(range => range.Holds(at)).OrderByDescending(range => range.Priority).Select(range => (uint?)range.Map).FirstOrDefault();
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
