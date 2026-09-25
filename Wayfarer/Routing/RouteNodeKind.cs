namespace Wayfarer.Routing;

/// <summary>What a fixed point in the world is, for routing purposes.</summary>
public enum RouteNodeKind
{
    /// <summary>A main aetheryte: can be teleported to from anywhere, once attuned.</summary>
    Aetheryte,

    /// <summary>An aethernet shard: reachable only on foot or by hopping from another node on the
    /// same network.</summary>
    Shard,

    /// <summary>Where the aethernet drops you with nothing there to board: just outside a city's
    /// gate, such as White Wolf Gate (Central Shroud). Hopped to from its network, never hopped
    /// from, and never teleported to.</summary>
    Landing,
}
