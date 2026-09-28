namespace Wayfarer.Modules.Treasure;

/// <summary>The metal last seen on each treasure spot. Saved as <c>treasure-memory.json</c>, apart
/// from the switches: it is learned, not chosen, and grows as the player plays.</summary>
internal sealed class TreasureMemoryConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The metal last seen at each spot, by <see cref="TreasureMemory.KeyOf"/>.</summary>
    public Dictionary<string, TreasureTier> Spots { get; set; } = [];
}
