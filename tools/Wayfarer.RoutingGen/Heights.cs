using System.Numerics;
using Lumina;
using Lumina.Excel.Sheets;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Gives every stop and door read off a map marker the real position and height the zone's
/// layout places it at.
///
/// <para>A map marker is only a spot on the map's picture, so everything read from one stood at
/// height zero. Routing then took a straight walk up a floor to be as short as the walk across it,
/// and the compass said "below" of a door on the street when the player stood level with it. The
/// layouts place the same things in the world, height and all.</para>
///
/// <para>An aetheryte or shard names its own spot in the layout: its Level references are pop
/// ranges there, not Level rows. A door drawn on a map is matched to the exit near it that leads to
/// the zone the door does, or failing that to the nearest spot of any kind. What finds nothing
/// keeps its marker's place, and is counted.</para></summary>
internal static class Heights
{
    /// <summary>How far across the ground an exit may be from a door's marker and still be its exit.</summary>
    private const float ExitReach = 25f;

    /// <summary>How far a spot may be from a door's marker and still say how high the door is.</summary>
    private const float SpotReach = 12f;

    public static List<RouteNode> Nodes(GameData game, ZoneLayouts layouts, IReadOnlyList<RouteNode> nodes)
    {
        var rows = game.Excel.GetSheet<Aetheryte>();
        var placed = 0;
        var result = new List<RouteNode>(nodes.Count);
        foreach (var node in nodes)
        {
            var layout = layouts.Of(node.At.Territory);
            var spot = rows.GetRowOrDefault(node.Id) is { } row
                ? row.Level.Select(level => level.RowId).FirstOrDefault(id => id != 0 && layout.Spots.ContainsKey(id))
                : 0u;

            if (spot != 0)
            {
                var at = layout.Spots[spot];
                result.Add(node with { At = node.At with { X = at.X, Y = at.Y, Z = at.Z } });
                placed++;
            }
            else
            {
                result.Add(node);
            }
        }

        Console.Error.WriteLine($"  {placed} of {nodes.Count} aetherytes and shards placed at their real height");
        return result;
    }

    /// <summary>Puts right the doors whose far side was never drawn. When a city's map draws no
    /// marker into an interior, the interior's own marker is all there is, and its far side was
    /// taken to be the same coordinates on the city's map, which in the city mean nothing: the Ruby
    /// Bazaar Offices' door came out in the middle of Kugane. The interior's exit is a warp whose
    /// landing spot is where the door really is, so that is taken instead, wherever there is one.</summary>
    public static List<DoorLink> Mirrored(GameData game, ZoneLayouts layouts, MapSpace maps, IReadOnlyList<DoorLink> doors)
    {
        var warps = game.Excel.GetSheet<Warp>();
        var fixedCount = 0;
        var mirrored = 0;
        var result = new List<DoorLink>(doors.Count);
        foreach (var door in doors)
        {
            var copied = door.Npc is null && door.From.Territory != door.To.Territory && door.From.X == door.To.X && door.From.Z == door.To.Z;
            if (!copied)
            {
                result.Add(door);
                continue;
            }

            mirrored++;
            var into = layouts.Of(door.To.Territory);
            Vector3? landing = null;
            foreach (var (id, _, _) in layouts.Of(door.From.Territory).Warpers)
            {
                if (warps.GetRowOrDefault(id) is { } warp && warp.TerritoryType.RowId == door.To.Territory && into.Spots.TryGetValue(warp.PopRange.RowId, out var spot))
                {
                    landing = spot;
                    break;
                }
            }

            if (landing is not { } at)
            {
                result.Add(door);
                continue;
            }

            fixedCount++;
            result.Add(door with { To = new Place(door.To.Territory, into.MapAt(at) ?? maps.MapOf(door.To.Territory), at.X, at.Y, at.Z) });
        }

        Console.Error.WriteLine($"  {fixedCount} of {mirrored} doors with a copied far side put where the door really is");
        return result;
    }

    public static List<DoorLink> Doors(ZoneLayouts layouts, IReadOnlyList<DoorLink> doors)
    {
        var raised = 0;
        var ends = 0;
        var result = new List<DoorLink>(doors.Count);
        foreach (var door in doors)
        {
            if (door.Npc is not null)
            {
                // Read straight off the layouts already.
                result.Add(door);
                continue;
            }

            ends += 2;
            var from = Raise(layouts, door.From, door.To.Territory, ref raised);
            var to = Raise(layouts, door.To, door.From.Territory, ref raised);
            result.Add(door with { From = from, To = to });
        }

        Console.Error.WriteLine($"  {raised} of {ends} door ends walked through given their real height");
        return result;
    }

    /// <summary>A door end at the height of the exit beside it that leads where the door does, or
    /// the nearest spot, or as it was.</summary>
    private static Place Raise(ZoneLayouts layouts, Place end, uint leadsTo, ref int raised)
    {
        if (end.Y != 0f)
        {
            // Already placed from the layouts.
            raised++;
            return end;
        }

        var layout = layouts.Of(end.Territory);
        var ground = new Vector2(end.X, end.Z);

        float? height = null;
        var nearest = float.MaxValue;
        foreach (var (at, leads) in layout.Exits)
        {
            var far = Vector2.Distance(ground, new Vector2(at.X, at.Z));
            if (far <= ExitReach && far < nearest && (leads == leadsTo || height is null))
            {
                (nearest, height) = (far, at.Y);
            }
        }

        if (height is null)
        {
            nearest = SpotReach;
            foreach (var at in layout.Spots.Values)
            {
                var far = Vector2.Distance(ground, new Vector2(at.X, at.Z));
                if (far <= nearest)
                {
                    (nearest, height) = (far, at.Y);
                }
            }
        }

        if (height is not { } y)
        {
            return end;
        }

        raised++;
        return end with { Y = y };
    }
}
