namespace Wayfarer.Core.Routing;

/// <summary>What a fixed point in the world is, for routing purposes.</summary>
public enum RouteNodeKind
{
    /// <summary>A main aetheryte: can be teleported to from anywhere, once attuned.</summary>
    Aetheryte,

    /// <summary>An aethernet shard: reachable only on foot or by hopping from another node on the
    /// same network.</summary>
    Shard,
}
