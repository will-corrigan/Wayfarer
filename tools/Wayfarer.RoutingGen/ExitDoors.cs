using System.Numerics;
using Lumina;
using Lumina.Excel.Sheets;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Every walk from one zone into another, read from the zones' own layouts.
///
/// <para>A zone's edge and a city's gate are exits: a box that, walked into, moves you to another
/// zone and drops you on a spot the exit names, a pop range in that zone's layout. Both ends are
/// placed in the world, height and all, so nothing about them is guessed. Each exit goes one way;
/// the way back is the exit on the other side, which is read from that zone in its turn.</para>
///
/// <para>The map markers that draw the same doors are only spots on a picture. They say where a
/// door is drawn and nothing of where it lands, which is what left far sides copied from the near
/// side and heights at zero. An exit replaces the marker wherever there is one.</para></summary>
internal static class ExitDoors
{
    /// <summary>How far across the ground from an exit's middle the ground beside it is looked for.</summary>
    private const float GroundReach = 40f;

    public static List<DoorLink> Read(GameData game, MapSpace maps, ZoneLayouts layouts, IReadOnlySet<uint> routable, IReadOnlyList<DoorLink> drawn)
    {
        var territories = game.Excel.GetSheet<TerritoryType>();
        var doors = new List<DoorLink>();
        var exits = 0;
        var unlanded = 0;
        var unplaced = 0;
        foreach (var territory in routable.Order())
        {
            var here = layouts.Of(territory);
            foreach (var (box, leads, lands, returns) in here.Exits)
            {
                if (!routable.Contains(leads) || leads == territory)
                {
                    continue;
                }

                exits++;
                if (lands == 0 || !layouts.Of(leads).Spots.TryGetValue(lands, out var landing))
                {
                    unlanded++;
                    continue;
                }

                // This side of the exit is where coming back through it lands: on the ground, just
                // inside the zone. The box's own middle can be fifty yalms up and a hundred across,
                // as the Heavensward zones' edges are. Without such a spot, the box's middle across
                // the ground at the floor's height there, or at a height marked unknown.
                var at = returns != 0 && here.Spots.TryGetValue(returns, out var back)
                    ? back
                    : box with { Y = here.FloorOf(here.MapAt(box) ?? maps.MapOf(territory), new Vector2(box.X, box.Z), GroundReach) ?? float.NaN };
                if (float.IsNaN(at.Y))
                {
                    unplaced++;
                }

                doors.Add(new DoorLink(
                    Name(drawn, territory, leads) ?? territories.GetRowOrDefault(leads)?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty,
                    Place(here, territory, at, maps),
                    Place(layouts.Of(leads), leads, landing, maps),
                    OneWay: true));
            }
        }

        Console.Error.WriteLine($"  {doors.Count} of {exits} exits between routable zones land on a spot the layout places; {unlanded} name none; {unplaced} stand where no ground height is known");
        return doors;
    }

    /// <summary>The doors the maps draw, less every one an exit or a warp already goes through.
    ///
    /// <para>A drawn door between two zones goes both ways. Where exits or warps go through it both
    /// ways, it is dropped. Where they go one way only, the other way is the same door walked back:
    /// from where going through it lands to where it was gone through from. That is how an interior
    /// entered by a door with a warp behind it is left, and both ends are placed. The drawn door's
    /// own ends are never kept for it, because its far side is often only a copy of its near side.
    /// Doors between the maps of one zone are not exits at all and are all kept.</para></summary>
    public static List<DoorLink> Unwalked(GameData game, IReadOnlyList<DoorLink> drawn, IReadOnlyList<DoorLink> walked)
    {
        var through = new Dictionary<(uint From, uint To), DoorLink>();
        foreach (var door in walked)
        {
            through.TryAdd((door.From.Territory, door.To.Territory), door);
        }

        var territories = game.Excel.GetSheet<TerritoryType>();
        var kept = new List<DoorLink>(drawn.Count);
        var replaced = 0;
        var walkedBack = 0;
        foreach (var door in drawn)
        {
            var (a, b) = (door.From.Territory, door.To.Territory);
            if (door.Npc is not null || a == b)
            {
                kept.Add(door);
                continue;
            }

            var there = through.GetValueOrDefault((a, b));
            var back = through.GetValueOrDefault((b, a));
            if (there is null && back is null)
            {
                kept.Add(door);
            }
            else if (door.OneWay || (there is not null && back is not null))
            {
                replaced++;
            }
            else
            {
                // Named for where it leads, as the drawn door is when it leads the same way.
                var gone = there ?? back!;
                var leads = gone.From.Territory;
                var name = leads == b ? door.Name : territories.GetRowOrDefault(leads)?.PlaceName.ValueNullable?.Name.ExtractText() ?? door.Name;
                kept.Add(new DoorLink(name, gone.To with { Radius = 0f }, gone.From with { Radius = 0f }, OneWay: true));
                walkedBack++;
            }
        }

        Console.Error.WriteLine($"  {replaced} drawn doors replaced by exits and warps, {walkedBack} walked back the way an exit or warp came, {kept.Count(door => door.From.Territory != door.To.Territory) - walkedBack} between zones kept as drawn");
        return kept;
    }

    /// <summary>What the map calls the door between two zones, from whichever side draws it.</summary>
    private static string? Name(IReadOnlyList<DoorLink> drawn, uint from, uint to) =>
        drawn.FirstOrDefault(door => door.From.Territory == from && door.To.Territory == to)?.Name
        ?? drawn.FirstOrDefault(door => door.From.Territory == to && door.To.Territory == from && !door.OneWay)?.Name;

    private static Place Place(ZoneLayout layout, uint territory, Vector3 at, MapSpace maps) =>
        new(territory, layout.MapAt(at) ?? maps.MapOf(territory), at.X, at.Y, at.Z);
}
