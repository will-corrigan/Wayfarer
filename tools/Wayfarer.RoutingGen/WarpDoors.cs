using System.Numerics;
using Lumina;
using Lumina.Excel.Sheets;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Every door that is a warp: a lift attendant, an airship's purser, a ferry's skipper,
/// and the doors and exits that are objects with a warp behind them. The sheets say what each one
/// sends you through; where it stands and where it sends you is in each zone's layout files.
///
/// <para>A warp names the zone it lands in and the spot, a pop range placed in that zone's layout.
/// Each end's map is the map range box of that zone the point stands in, the same boxes the game
/// uses to say which map the player is on: a lift in Ul'dah lands on the airship landing's own map,
/// a floor above the street.</para>
///
/// <para>Warps that cost gil are kept: paying is not a gap. Warps kept until quests are done carry
/// those quests, and a route only takes them once the player has done them. Every warp is one way;
/// the way back is whoever or whatever stands at the other end.</para></summary>
internal static class WarpDoors
{
    public static List<DoorLink> Read(GameData game, MapSpace maps, ZoneLayouts layouts, IReadOnlySet<uint> routable)
    {
        var names = game.Excel.GetSheet<ENpcResident>();
        var warps = game.Excel.GetSheet<Warp>();
        var territories = game.Excel.GetSheet<TerritoryType>();

        var doors = new List<DoorLink>();
        foreach (var territory in routable.Order())
        {
            var here = layouts.Of(territory);
            foreach (var (id, person, standing, festival, phase) in here.Warpers)
            {
                if (warps.GetRowOrDefault(id) is not { } warp)
                {
                    continue;
                }

                var landsIn = warp.TerritoryType.RowId;
                if (!routable.Contains(landsIn) || !layouts.Of(landsIn).Spots.TryGetValue(warp.PopRange.RowId, out var landing))
                {
                    continue;
                }

                // The door is the warp and whoever offers it, by their rows. What it is called is
                // only words, and always what to do: the warp's own name, or the question the game
                // asks before it ("Travel to the Doman Enclave?"), or else travelling to where it
                // leads, put the way the game puts its own questions. A warp with none of those is
                // still a door: a skipper whose warp had no name was once left out, and with it the
                // only boat from Yanxia to the Doman Enclave.
                var asked = person != 0 ? Shown(names.GetRowOrDefault(person)?.Singular.ExtractText() ?? string.Empty) : null;
                var name = warp.Name.ExtractText() is { Length: > 0 } named ? named
                    : warp.Question.ExtractText().TrimEnd('?') is { Length: > 0 } question ? question
                    : $"Travel to {territories.GetRowOrDefault(landsIn)?.PlaceName.ValueNullable?.Name.ExtractText()}";

                doors.Add(new DoorLink(
                    name,
                    Place(here, territory, standing, maps),
                    Place(layouts.Of(landsIn), landsIn, landing, maps),
                    OneWay: true,
                    Npc: asked,
                    Quests: Quests(warp.WarpCondition.ValueNullable),
                    Warp: id,
                    Festival: festival,
                    FestivalPhase: phase,
                    Person: person));
            }
        }

        Console.Error.WriteLine($"  {doors.Count(door => door.Person != 0)} doors taken by asking, {doors.Count(door => door.Person == 0)} doors and exits that are warps");
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
    /// height the layout places it.</summary>
    private static Place Place(ZoneLayout layout, uint territory, Vector3 at, MapSpace maps)
    {
        var map = layout.MapAt(at) ?? maps.MapOf(territory);
        return new Place(territory, map, at.X, at.Y, at.Z);
    }
}
