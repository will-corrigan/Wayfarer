using System.Text;
using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Presentation;

/// <summary>The one line of words a surface prints about a route: the legs the player has to do
/// something about, in order. Walking is never mentioned, because the needle and the distance
/// say that better than a word could; a route that is only a walk has no words at all.</summary>
public static class RouteWords
{
    /// <summary>What the line says when the target is known but no route to it was found: the
    /// graph has no way there from here, usually because the routing data is missing a link.</summary>
    public const string NoRoute = "No route from here";

    private const string TeleportPhrase = "Teleport to ";
    private const string ShardHopPhrase = "Aethernet to ";
    private const string DoorPhrase = "Through ";
    private const string Joiner = ", then ";

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
