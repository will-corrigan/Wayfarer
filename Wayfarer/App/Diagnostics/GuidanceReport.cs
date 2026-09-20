using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
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
internal sealed class GuidanceReport(IGuidance guidance, IObjectTable objects, IClientState client, IChatGui chat, IObjectFinder finder)
{
    /// <summary>Writes the report to the chat log, one line per fact.</summary>
    public void Print()
    {
        chat.Print("[Wayfarer] --- why ---");
        if (objects.LocalPlayer is not { } player)
        {
            chat.Print("no local player");
            return;
        }

        var at = player.Position;
        chat.Print($"you: territory {client.TerritoryType} map {client.MapId} at ({Num(at.X)}, {Num(at.Y)}, {Num(at.Z)})");

        if (guidance.Current is not { } now)
        {
            chat.Print("nothing is being guided");
            return;
        }

        chat.Print($"holder {now.Source.Name}: {now.Objective.Headline}");
        if (now.Target is not { } target)
        {
            chat.Print("no entry of it can be reached");
            return;
        }

        chat.Print($"entry: {target.Text}");
        switch (target.Where)
        {
            case Destination.Reachable reachable:
                Describe(reachable, at, now.Route);
                break;
            case Destination.InDuty duty:
                chat.Print($"where: inside duty {duty.DutyId}");
                break;
            case Destination.Blocked blocked:
                chat.Print($"where: blocked - {blocked.Reason}");
                break;
        }
    }

    private static string Num(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>How far across the ground, which is how a circle is judged.</summary>
    private static float Flat(Vector3 from, float toX, float toZ)
    {
        var (dx, dz) = (from.X - toX, from.Z - toZ);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

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

    /// <summary>Every place the step offered, how far each is by its middle and by its edge, which
    /// one the route chose, and what the finder sees inside the one it walks to.</summary>
    private void Describe(Destination.Reachable reachable, Vector3 at, Route? route)
    {
        chat.Print($"places offered: {reachable.Places.Count}");
        foreach (var place in reachable.Places)
        {
            var apart = Flat(at, place.X, place.Z);
            var chosen = route is { } taken && taken.End == place ? "  <= ROUTED HERE" : string.Empty;
            chat.Print($"  {Spell(place)}  middle {Num(apart)}y  edge {Num(MathF.Max(0f, apart - place.Radius))}y{chosen}");
        }

        chat.Print($"marks: {(reachable.Marks is not { Count: > 0 } marks ? "none" : string.Join(", ", marks.Select(mark => $"{mark.Id}/{mark.Kind}")))}");
        chat.Print($"owner event: {(reachable.Owner is { } owner ? ((uint)owner).ToString(CultureInfo.InvariantCulture) : "none")}, expected places: {reachable.Expected?.Count ?? 0}");

        if (route is null)
        {
            chat.Print("route: none");
            return;
        }

        chat.Print($"route: cost {Num(route.Cost)} - {string.Join(" | ", route.Legs.Select(Spell))}");
        if (route.Legs is [Leg.Walk walk, ..])
        {
            var found = finder.Inside(walk.To, reachable.Marks, reachable.Owner, reachable.Expected);
            chat.Print(found is null
                ? "finder: nothing of the step's is standing in that place"
                : $"finder: aiming at ({Num(found.X)}, {Num(found.Y)}, {Num(found.Z)}), {Num(Flat(at, found.X, found.Z))}y off");
        }
    }
}
