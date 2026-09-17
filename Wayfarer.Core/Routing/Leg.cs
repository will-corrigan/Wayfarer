namespace Wayfarer.Core.Routing;

/// <summary>One step of a route, and what it costs. A route is a list of these; the total is the
/// sum. Every kind carries only what a surface needs to say it, never how it was chosen.
///
/// <para>The costs are in yalms-equivalent so that a teleport across the world and a walk across a
/// field compare directly. The two fixed overheads are the cast-and-load pause of a teleport and
/// the load pause of a shard hop; walking costs its distance; a door costs nothing of its own,
/// the walks either side of it carry the distance.</para></summary>
public abstract record Leg
{
    /// <summary>What a teleport costs before any walking: the cast and the loading screen.</summary>
    public const float TeleportCost = 120f;

    /// <summary>What a shard hop costs before any walking: the loading pause between shards.</summary>
    public const float ShardHopCost = 60f;

    private Leg()
    {
    }

    /// <summary>This leg's cost in yalms-equivalent.</summary>
    public abstract float Cost { get; }

    /// <summary>Walk this far, on the map you are on.</summary>
    public sealed record Walk(float Yalms) : Leg
    {
        /// <inheritdoc/>
        public override float Cost => Yalms;
    }

    /// <summary>Teleport to an aetheryte you are attuned to. The next leg starts where it drops
    /// you.</summary>
    public sealed record Teleport(uint AetheryteId, string AetheryteName) : Leg
    {
        /// <inheritdoc/>
        public override float Cost => TeleportCost;
    }

    /// <summary>Enter one shard of a city's aethernet and leave by another on the same network.
    /// The walks to the entry and from the exit are their own legs.</summary>
    public sealed record ShardHop(string EntryShard, string ExitShard) : Leg
    {
        /// <inheritdoc/>
        public override float Cost => ShardHopCost;
    }

    /// <summary>Pass through a door between two maps: a building's entrance, a staircase between
    /// floors.</summary>
    public sealed record Door(string Name) : Leg
    {
        /// <inheritdoc/>
        public override float Cost => 0f;
    }
}
