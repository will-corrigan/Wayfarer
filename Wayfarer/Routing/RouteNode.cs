namespace Wayfarer.Routing;

/// <summary>A fixed point the routing graph knows about: an aetheryte or an aethernet shard. Built
/// once from the game's data and never changed while playing.</summary>
/// <param name="Id">The game's own id for it — the Aetheryte sheet row — which is also the id the
/// attunement check is asked about.</param>
/// <param name="Name">What a surface calls it.</param>
/// <param name="Kind">Whether it can be teleported to.</param>
/// <param name="Network">The aethernet network it belongs to, or zero for none. Any two nodes on
/// the same non-zero network can be hopped between. A city's main aetheryte is on its network
/// too, which is what lets a route teleport in and then hop.</param>
/// <param name="At">Where it stands.</param>
public sealed record RouteNode(uint Id, string Name, RouteNodeKind Kind, uint Network, Place At);
