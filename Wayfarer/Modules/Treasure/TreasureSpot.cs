using System.Numerics;

namespace Wayfarer.Modules.Treasure;

/// <summary>A place the zone's layout puts treasure: a dungeon's chest, a field coffer's spawn point.</summary>
/// <param name="Territory">The zone.</param>
/// <param name="Map">The map it is drawn on: the floor it stands on, in a zone of several.</param>
/// <param name="Position">Where it stands, in world space.</param>
internal sealed record TreasureSpot(uint Territory, uint Map, Vector3 Position);
