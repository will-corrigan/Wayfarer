using System.Numerics;

namespace Wayfarer.Modules.Treasure;

/// <summary>A place a treasure coffer can appear.</summary>
/// <param name="Territory">The zone.</param>
/// <param name="Map">The map it is drawn on.</param>
/// <param name="Position">Where it stands, in world space.</param>
internal sealed record CofferSpot(uint Territory, uint Map, Vector3 Position);
