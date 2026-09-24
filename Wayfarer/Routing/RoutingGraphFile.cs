using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wayfarer.Routing;

/// <summary>The shipped routing data: every fixed node and door, as JSON. Written by the
/// generator from the game's sheets, committed, and read by the plugin at load. The shape is the
/// graph's own records, so there is nothing to translate on either side.</summary>
/// <param name="Nodes">Every aetheryte and shard.</param>
/// <param name="Doors">Every door between two maps.</param>
public sealed record RoutingGraphFile(IReadOnlyList<RouteNode> Nodes, IReadOnlyList<DoorLink> Doors)
{
    /// <summary>The file's name wherever it is shipped or generated.</summary>
    public const string FileName = "routing-graph.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },

        // A height nothing could place is NaN, not zero, so routing and the compass know it is
        // unknown rather than take it for a floor.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>The file's contents as a graph.</summary>
    public RouteGraph ToGraph() => new(Nodes, Doors);

    /// <summary>Parses <paramref name="json"/>. Throws when it is not a routing graph file.</summary>
    public static RoutingGraphFile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<RoutingGraphFile>(json, Options)
            ?? throw new JsonException("The routing graph file is empty.");
    }

    /// <summary>This file as JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);
}
