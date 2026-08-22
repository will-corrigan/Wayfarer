namespace Wayfarer.Core.Hunting;

/// <summary>One remaining hunting-log target with its resolved world position — the plugin-side
/// reader converts a curated <see cref="HuntingLocation"/> map coordinate to world space (via
/// <c>Navigation.MapCoords.MapToWorld</c> against the live <c>Map</c> sheet row) before calling
/// into <see cref="HuntingChaining"/>, the same "resolve to world coords first, then order" split
/// <c>Unlocks.RoutePlanner</c> uses for unlock pickups.</summary>
public sealed record HuntingChainTarget(HuntingMonster Monster, uint TerritoryTypeId, float WorldX, float WorldZ);
