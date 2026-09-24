namespace Wayfarer.Routing;

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

    /// <summary>Walk to <paramref name="To"/>, on the map you are on. The one leg the compass can
    /// point at: when a walk is first, the needle and the distance are to its end.</summary>
    /// <param name="To">Where the walk ends: the next node, door side or target.</param>
    /// <param name="Yalms">How far, from where the walk starts.</param>
    public sealed record Walk(Place To, float Yalms) : Leg
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
    /// floors, or a lift or airship taken by asking someone.</summary>
    /// <param name="Name">What the door is called, or for one taken by asking, what is asked for.</param>
    /// <param name="Npc">Who to ask, or null for a door walked through.</param>
    public sealed record Door(string Name, string? Npc = null) : Leg
    {
        /// <inheritdoc/>
        /// <remarks>A door walked through costs nothing of its own. One taken by asking costs the
        /// talk and the loading pause that follows, as much as a shard hop.</remarks>
        public override float Cost => Npc is null ? 0f : ShardHopCost;
    }
}
