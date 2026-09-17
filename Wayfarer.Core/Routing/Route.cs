namespace Wayfarer.Core.Routing;

/// <summary>The cheapest way from where the player stands to one of the places they were offered.
/// </summary>
/// <param name="Legs">The steps in order. Never empty: a player already standing at the place gets
/// one walk of zero.</param>
/// <param name="Cost">The sum of the legs' costs.</param>
/// <param name="End">Which of the offered places this route reaches.</param>
public sealed record Route(IReadOnlyList<Leg> Legs, float Cost, Place End);
