using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Wayfarer.Guidance;
using Wayfarer.Routing;

namespace Wayfarer.App.Diagnostics;

/// <summary>Says out loud what the app believes right now: where the player stands, what the step
/// offered, how far each of those places is both by its middle and by its edge, which one routing
/// chose, and what the finder can see standing in it. Printed to the chat log by
/// <c>/wayfarer why</c>.
///
/// <para>Guidance pointing somewhere wrong looks the same from outside whatever the cause: a place
/// the data never meant, the wrong choice between two it did mean, or the wrong thing found inside
/// the right one. None of those can be told apart from a screenshot, and the numbers that separate
/// them are all already in hand at the moment the needle is drawn. This prints them.</para></summary>
internal sealed unsafe class GuidanceReport(IGuidance guidance, IObjectTable objects, IClientState client, IChatGui chat, IPluginLog log)
{
    /// <summary>How many of the things standing in a place are worth listing before the point
    /// is made.</summary>
    private const int NearbyShown = 40;

    /// <summary>Writes the report, one line per fact. Every line goes to the plugin log, where it
    /// can be read back after the fact and copied out of; the chat only says where to look, since
    /// a report long enough to be useful is too long to read as it scrolls past.</summary>
    public void Print()
    {
        // Every line of this reads the game's own memory while the player is standing in it, and a
        // report is asked for precisely when something is already not as expected. Nothing it finds
        // is worth taking the session down for, and the command that asks for it cannot catch
        // anything itself: it hands the work to the framework thread and lets go of it.
        try
        {
            Report();
        }
        catch (Exception ex)
        {
            log.Error(ex, "the report could not be written.");
        }
    }

    private static string Num(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>How far across the ground, which is how a circle is judged.</summary>
    private static float Flat(Vector3 from, float toX, float toZ) =>
        Vector2.Distance(new Vector2(from.X, from.Z), new Vector2(toX, toZ));

    private static string Spell(Place place) =>
        $"terr {place.Territory} map {place.Map} ({Num(place.X)}, {Num(place.Y)}, {Num(place.Z)}) r={Num(place.Radius)}";

    private static string Spell(Leg leg) => leg switch
    {
        Leg.Walk walk => $"walk {Num(walk.Yalms)}y to ({Num(walk.To.X)}, {Num(walk.To.Z)}) r={Num(walk.To.Radius)}",
        Leg.Teleport teleport => $"teleport to {teleport.AetheryteName}",
        Leg.ShardHop hop => $"shard {hop.EntryShard} -> {hop.ExitShard}",
        Leg.Door door => $"door {door.Name}",
        _ => "?",
    };

    private void Report()
    {
        Say("--- why ---");
        Markers();
        chat.Print("[Wayfarer] report written to the Dalamud log.");
        if (objects.LocalPlayer is not { } player)
        {
            Say("no local player");
            return;
        }

        var at = player.Position;
        Say($"you: territory {client.TerritoryType} map {client.MapId} at ({Num(at.X)}, {Num(at.Y)}, {Num(at.Z)})");

        if (guidance.Current is not { } now)
        {
            Say("nothing is being guided");
            return;
        }

        Say($"holder {now.Source.Name}: {now.Objective.Headline}");
        if (now.Target is not { } target)
        {
            Say("no entry of it can be reached");
            return;
        }

        Say($"entry: {target.Text}");
        switch (target.Where)
        {
            case Destination.Reachable reachable:
                Describe(reachable.Places, at, now.Route);
                break;
            case Destination.AtObject thing:
                Say($"where: the thing the module picked, id {thing.Id:X}");
                Describe([thing.At], at, now.Route);
                break;
            case Destination.InDuty duty:
                Say($"where: inside duty {duty.DutyId}");
                break;
            case Destination.Blocked blocked:
                Say($"where: blocked - {blocked.Reason}");
                break;
        }
    }

    /// <summary>Everything standing in the place being walked to, with what the finder asks of
    /// each: the id the world gives it, the event the game says spawned it, and whether the player
    /// could act on it now. When the finder says it can see nothing, this says what was there.
    /// </summary>
    private void Nearby(Place area)
    {
        if (area.Radius <= 0f)
        {
            return;
        }

        var shown = 0;
        foreach (var candidate in objects)
        {
            var position = candidate.Position;
            if (Flat(position, area.X, area.Z) > area.Radius)
            {
                continue;
            }

            var raw = candidate.Address == 0 ? null : (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)candidate.Address;
            var stamp = raw == null ? 0u : (uint)raw->EventId;
            var plate = raw == null ? 0u : raw->NamePlateIconId;
            var state = raw == null ? (byte)0 : raw->EventState;

            Say($"  in place: base {candidate.BaseId} kind {candidate.ObjectKind} event {stamp} plate {plate} state {state} targetable {candidate.IsTargetable} '{candidate.Name}' {Num(Flat(position, area.X, area.Z))}y from the middle");
            if (++shown >= NearbyShown)
            {
                Say("  in place: ... and more");
                break;
            }
        }

        Say($"in place: {shown} of what the client has loaded");
    }

    /// <summary>One line of the report.</summary>
    private void Say(string line) => log.Information("[why] {Line}", line);

    /// <summary>Every marker the game is drawing on the map right now, with everything it carries
    /// about each one. Nothing here knows what a quest is: this is what the game says it is
    /// showing the player, which is the only account of a marker that cannot be second-guessed.
    /// </summary>
    private void Markers()
    {
        var map = Map.Instance();
        if (map == null)
        {
            Say("map: none");
            return;
        }

        var shown = 0;
        foreach (ref var info in map->QuestMarkers)
        {
            for (var i = 0; i < (int)info.MarkerData.LongCount; i++)
            {
                ref var data = ref info.MarkerData[i];
                var tip = data.TooltipString == null ? string.Empty : data.TooltipString->ToString();
                Say($"marker objective {info.ObjectiveId:X8}/{data.ObjectiveId:X8} label '{info.Label}' | level {data.LevelId} type {data.MarkerType} icon {data.IconId} dataId {data.DataId} state {data.EventState} flags {data.Flags} r={Num(data.Radius)} terr {data.TerritoryTypeId} at ({Num(data.Position.X)}, {Num(data.Position.Z)}) '{tip}'");
                shown++;
            }
        }

        Say($"markers drawn: {shown}");
    }

    /// <summary>Every place the entry offered, how far each is by its middle and by its edge,
    /// which one the route chose, and what is standing in the one it walks to.</summary>
    private void Describe(IReadOnlyList<Place> places, Vector3 at, Route? route)
    {
        Say($"places offered: {places.Count}");
        foreach (var place in places)
        {
            var apart = Flat(at, place.X, place.Z);
            var chosen = route is { } taken && taken.End == place ? "  <= ROUTED HERE" : string.Empty;
            Say($"  {Spell(place)}  middle {Num(apart)}y  edge {Num(MathF.Max(0f, apart - place.Radius))}y{chosen}");
        }

        if (route is null)
        {
            Say("route: none");
            return;
        }

        Say($"route: cost {Num(route.Cost)} - {string.Join(" | ", route.Legs.Select(Spell))}");
        if (route.Legs is [Leg.Walk walk, ..])
        {
            Nearby(walk.To);
        }
    }
}
