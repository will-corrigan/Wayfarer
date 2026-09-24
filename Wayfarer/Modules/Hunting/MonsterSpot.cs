namespace Wayfarer.Modules.Hunting;

/// <summary>One spot a monster has been seen.</summary>
/// <param name="Territory">The zone.</param>
/// <param name="Map">The map within it.</param>
/// <param name="X">World X.</param>
/// <param name="Z">World Z.</param>
internal sealed record MonsterSpot(uint Territory, uint Map, float X, float Z);
