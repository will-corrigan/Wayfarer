using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Tells the aethernet's stops that can be boarded from the ones it only drops you at.
///
/// <para>A city's aethernet lists places just outside its gates, such as White Wolf Gate (Central
/// Shroud): choosing one drops you there, and there is no shard there to hop back from. The sheets
/// give such a stop a row, a network and a landing spot like any shard; only the zone's layout says
/// the difference, by placing an aetheryte object for every stop that can be boarded and none for
/// these. A stop with nothing placed is a landing, hopped to and never from.</para></summary>
internal static class Landings
{
    public static List<RouteNode> Mark(ZoneLayouts layouts, IReadOnlyList<RouteNode> nodes)
    {
        var result = new List<RouteNode>(nodes.Count);
        var landings = new List<string>();
        var unplacedAetherytes = new List<string>();
        foreach (var node in nodes)
        {
            var placed = layouts.Of(node.At.Territory).Aetherytes.Contains(node.Id);
            if (node.Kind == RouteNodeKind.Shard && !placed)
            {
                landings.Add($"{node.Id} {node.Name} ({node.At.Territory})");
                result.Add(node with { Kind = RouteNodeKind.Landing });
                continue;
            }

            if (node.Kind == RouteNodeKind.Aetheryte && !placed)
            {
                // Nothing to board where a main aetheryte stands would mean the layouts were not
                // read, not that it cannot be used; it is kept, and named.
                unplacedAetherytes.Add($"{node.Id} {node.Name} ({node.At.Territory})");
            }

            result.Add(node);
        }

        Console.Error.WriteLine($"  {landings.Count} aethernet stops with nothing to board, hopped to and never from:");
        foreach (var landing in landings)
        {
            Console.Error.WriteLine($"    {landing}");
        }

        Console.Error.WriteLine($"  {unplacedAetherytes.Count} aetherytes the layouts place nothing for{(unplacedAetherytes.Count == 0 ? string.Empty : ":")}");
        foreach (var aetheryte in unplacedAetherytes)
        {
            Console.Error.WriteLine($"    {aetheryte}");
        }

        return result;
    }
}
