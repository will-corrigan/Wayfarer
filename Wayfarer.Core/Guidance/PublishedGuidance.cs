using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Guidance;

/// <summary>One published guidance: whose objective it is, the objective, and the route to its
/// cheapest reachable place. What every surface reads. Published only when the words or the
/// shape of the route change; the numbers that change every frame — distance, bearing — are read
/// separately by the surfaces that draw them.</summary>
/// <param name="Source">The module that is guiding.</param>
/// <param name="Objective">What it is guiding to.</param>
/// <param name="Route">How to get there, or null when nothing in the objective can be routed.</param>
public sealed record PublishedGuidance(IObjectiveSource Source, Objective Objective, Route? Route);
