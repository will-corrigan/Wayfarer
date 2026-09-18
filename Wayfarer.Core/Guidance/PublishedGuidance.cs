using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Guidance;

/// <summary>One published guidance: whose objective it is, the objective, which of its entries
/// is being routed to, and the route there. What every surface reads. Published only when the
/// words or the shape of the route change; the numbers that change every frame — distance,
/// bearing — are read separately by the surfaces that draw them.</summary>
/// <param name="Source">The module that is guiding.</param>
/// <param name="Objective">What it is guiding to.</param>
/// <param name="Target">The entry the route is for — <see cref="Guidance.Objective.FirstReachable"/>
/// — so a surface knows which line the needle belongs to. Null when no entry can be reached.</param>
/// <param name="Route">How to get to the target, or null when there is no target or no way there.</param>
public sealed record PublishedGuidance(IObjectiveSource Source, Objective Objective, ObjectiveEntry? Target, Route? Route);
