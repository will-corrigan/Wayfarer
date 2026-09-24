using System.Text;
using Wayfarer.Guidance;
using Wayfarer.Routing;

namespace Wayfarer.Presentation;

/// <summary>Composes the one line of words a surface prints about a route: the legs the player
/// has to do something about, in order, with the mark and the press that go with the first of
/// them. Walking is never mentioned, because the needle and the distance say that better than a
/// word could; a route that is only a walk has no line at all.</summary>
public static class RouteWords
{
    /// <summary>What the line says when the target is known but no route to it was found: the
    /// graph has no way there from here, usually because the routing data is missing a link.</summary>
    public const string NoRoute = "No route from here";

    /// <summary>What the line says when the target is inside a duty.</summary>
    public const string DutyWords = "Open the Duty Finder";

    private const string TeleportPhrase = "Teleport to ";
    private const string ShardHopPhrase = "Aethernet to ";
    private const string DoorPhrase = "Through ";
    private const string Joiner = ", then ";

    /// <summary>The route line for this guidance, or null when there is nothing to say: no target,
    /// or a route that is only a walk.</summary>
    public static RouteLine? Compose(PublishedGuidance? guidance)
    {
        if (guidance?.Target is not { } target)
        {
            return null;
        }

        if (target.Where is Destination.InDuty duty)
        {
            return new RouteLine(RouteGlyph.Duty, DutyWords, new RoutePress.OpenDuty(duty.DutyId), DutyWords);
        }

        if (guidance.Route is not { } route)
        {
            return new RouteLine(RouteGlyph.None, NoRoute, null);
        }

        if (Describe(route) is not { } words)
        {
            return null;
        }

        return route.Legs[0] is Leg.Teleport teleport
            ? new RouteLine(RouteGlyph.Aetheryte, words, new RoutePress.Teleport(teleport.AetheryteId), TeleportPhrase + teleport.AetheryteName)
            : new RouteLine(RouteGlyph.None, words, null);
    }

    /// <summary>The route's words, or null when it is only a walk.</summary>
    public static string? Describe(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var words = new StringBuilder();
        foreach (var leg in route.Legs)
        {
            var phrase = leg switch
            {
                Leg.Teleport teleport => TeleportPhrase + teleport.AetheryteName,
                Leg.ShardHop hop => ShardHopPhrase + hop.ExitShard,
                Leg.Door { Npc: { } npc } asked => $"{asked.Name} ({npc})",
                Leg.Door door => DoorPhrase + door.Name,
                _ => null,
            };

            if (phrase is null)
            {
                continue;
            }

            if (words.Length > 0)
            {
                words.Append(Joiner);
            }

            words.Append(phrase);
        }

        return words.Length == 0 ? null : words.ToString();
    }
}
