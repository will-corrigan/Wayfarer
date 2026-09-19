namespace Wayfarer.Routing;

/// <summary>Every fixed way of moving through the world, and the search that finds the cheapest
/// path across it.
///
/// <para><b>What is stored and what is not.</b> The graph holds only what the game's data fixes:
/// aetherytes, shards, doors, and the hops between nodes on one aethernet network. Walking and
/// teleporting are not stored as edges, because they exist between too many pairs to list: a
/// walk is possible between any two points on the same map, and a teleport from anywhere to any
/// attuned aetheryte. Both are generated at the moment the search considers a node.</para>
///
/// <para><b>What changes per search.</b> The player's position and the offered places are added as
/// temporary nodes for one search and discarded. Attunement is a check the search is handed, asked
/// as each teleport or hop edge is considered, so attuning a new aetheryte changes the next answer
/// with no rebuild.</para>
///
/// <para><b>Several places.</b> The search settles nodes cheapest-first and stops at the first
/// offered place it settles, which is by construction the cheapest one to reach. Comparing a
/// teleport-then-walk to one place against a plain walk to another is therefore the same
/// comparison as everything else.</para></summary>
public sealed class RouteGraph
{
    private readonly RouteNode[] nodes;
    private readonly List<StaticEdge>[] edges;
    private readonly DoorEnd[] doorEnds;

    /// <summary>Initializes a new instance of the <see cref="RouteGraph"/> class from the game's fixed
    /// points and doors.</summary>
    public RouteGraph(IReadOnlyList<RouteNode> fixedNodes, IReadOnlyList<DoorLink> doors)
    {
        ArgumentNullException.ThrowIfNull(fixedNodes);
        ArgumentNullException.ThrowIfNull(doors);

        // Each door contributes two nodes, one per side, so a walk can end at a door and the
        // door's own edge carries you to the other side.
        nodes = new RouteNode[fixedNodes.Count];
        doorEnds = new DoorEnd[doors.Count * 2];
        edges = new List<StaticEdge>[fixedNodes.Count + doorEnds.Length];
        for (var i = 0; i < edges.Length; i++)
        {
            edges[i] = [];
        }

        for (var i = 0; i < fixedNodes.Count; i++)
        {
            nodes[i] = fixedNodes[i];
        }

        for (var i = 0; i < doors.Count; i++)
        {
            var door = doors[i];
            var a = fixedNodes.Count + (i * 2);
            var b = a + 1;
            doorEnds[i * 2] = new DoorEnd(door.Name, door.From);
            doorEnds[(i * 2) + 1] = new DoorEnd(door.Name, door.To);
            edges[a].Add(new StaticEdge(b, new Leg.Door(door.Name)));
            if (!door.OneWay)
            {
                edges[b].Add(new StaticEdge(a, new Leg.Door(door.Name)));
            }
        }

        // Hops: every pair on one network. A city's main aetheryte is on its network, so a route
        // can teleport in and hop out; attunement of both ends is checked at search time.
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Network == 0)
            {
                continue;
            }

            for (var j = 0; j < nodes.Length; j++)
            {
                if (j != i && nodes[j].Network == nodes[i].Network)
                {
                    edges[i].Add(new StaticEdge(j, new Leg.ShardHop(nodes[i].Name, nodes[j].Name)));
                }
            }
        }
    }

    /// <summary>The cheapest route from <paramref name="from"/> to any of <paramref name="targets"/>,
    /// or null when none can be reached at all.</summary>
    /// <param name="from">Where the player stands.</param>
    /// <param name="targets">The places on offer. The route ends at whichever is cheapest to reach.</param>
    /// <param name="attuned">Whether the player can use the aetheryte or shard with this id.</param>
    public Route? FindRoute(Place from, IReadOnlyList<Place> targets, Func<uint, bool> attuned)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(attuned);

        if (targets.Count == 0)
        {
            return null;
        }

        var search = new Search(this, from, targets, attuned);
        return search.Run();
    }

    private static float Distance(Place a, Place b) =>
        MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    private static bool SameMap(Place a, Place b) => a.Territory == b.Territory && a.Map == b.Map;

    private readonly record struct StaticEdge(int To, Leg Leg);

    private readonly record struct DoorEnd(string Name, Place At);

    /// <summary>One search over the graph plus that search's temporary nodes: the origin and the
    /// targets. Indexes are laid out as fixed nodes, then door sides, then the origin, then the
    /// targets, so the static adjacency lists stay valid and the temporary nodes are simply the
    /// tail.</summary>
    private sealed class Search
    {
        private readonly RouteGraph graph;
        private readonly Func<uint, bool> attuned;
        private readonly Place[] places;
        private readonly int origin;
        private readonly int firstTarget;
        private readonly float[] cost;
        private readonly int[] cameFrom;
        private readonly Leg?[] cameBy;
        private readonly bool[] settled;

        public Search(RouteGraph graph, Place from, IReadOnlyList<Place> targets, Func<uint, bool> attuned)
        {
            this.graph = graph;
            this.attuned = attuned;

            var fixedCount = graph.edges.Length;
            places = new Place[fixedCount + 1 + targets.Count];
            for (var i = 0; i < graph.nodes.Length; i++)
            {
                places[i] = graph.nodes[i].At;
            }

            for (var i = 0; i < graph.doorEnds.Length; i++)
            {
                places[graph.nodes.Length + i] = graph.doorEnds[i].At;
            }

            origin = fixedCount;
            places[origin] = from;
            firstTarget = origin + 1;
            for (var i = 0; i < targets.Count; i++)
            {
                places[firstTarget + i] = targets[i];
            }

            cost = new float[places.Length];
            Array.Fill(cost, float.PositiveInfinity);
            cameFrom = new int[places.Length];
            Array.Fill(cameFrom, -1);
            cameBy = new Leg?[places.Length];
            settled = new bool[places.Length];
        }

        public Route? Run()
        {
            var queue = new PriorityQueue<int, (float Cost, int Order)>();
            var order = 0;
            cost[origin] = 0f;
            queue.Enqueue(origin, (0f, order++));

            while (queue.TryDequeue(out var node, out _))
            {
                if (settled[node])
                {
                    continue;
                }

                settled[node] = true;
                if (node >= firstTarget)
                {
                    return Build(node);
                }

                foreach (var (to, leg) in Neighbours(node))
                {
                    var through = cost[node] + leg.Cost;
                    if (through < cost[to])
                    {
                        cost[to] = through;
                        cameFrom[to] = node;
                        cameBy[to] = leg;
                        queue.Enqueue(to, (through, order++));
                    }
                }
            }

            return null;
        }

        /// <summary>Everywhere one step can lead from <paramref name="node"/>: the stored hops and
        /// doors, a walk to anything on the same map, and a teleport to any attuned aetheryte.
        /// </summary>
        private IEnumerable<(int To, Leg Leg)> Neighbours(int node)
        {
            if (node < graph.edges.Length)
            {
                foreach (var edge in graph.edges[node])
                {
                    if (edge.Leg is Leg.ShardHop && !(IsAttuned(node) && IsAttuned(edge.To)))
                    {
                        continue;
                    }

                    yield return (edge.To, edge.Leg);
                }
            }

            var here = places[node];
            for (var other = 0; other < places.Length; other++)
            {
                if (other == node || other == origin)
                {
                    continue;
                }

                if (SameMap(here, places[other]))
                {
                    yield return (other, new Leg.Walk(places[other], Distance(here, places[other])));
                }
            }

            for (var i = 0; i < graph.nodes.Length; i++)
            {
                var candidate = graph.nodes[i];
                if (i != node && candidate.Kind == RouteNodeKind.Aetheryte && attuned(candidate.Id))
                {
                    yield return (i, new Leg.Teleport(candidate.Id, candidate.Name));
                }
            }
        }

        private bool IsAttuned(int node) => node < graph.nodes.Length && attuned(graph.nodes[node].Id);

        private Route Build(int target)
        {
            var legs = new List<Leg>();
            for (var at = target; cameFrom[at] >= 0; at = cameFrom[at])
            {
                legs.Add(cameBy[at]!);
            }

            legs.Reverse();
            if (legs.Count == 0)
            {
                legs.Add(new Leg.Walk(places[target], 0f));
            }

            return new Route(legs, cost[target], places[target]);
        }
    }
}
