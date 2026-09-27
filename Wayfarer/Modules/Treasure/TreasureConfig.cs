namespace Wayfarer.Modules.Treasure;

/// <summary>The treasure module's own settings. Saved as <c>treasure.json</c>.</summary>
internal sealed class TreasureConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Whether every place a coffer can appear in the Occult Crescent is marked on the map.</summary>
    public bool ShowCofferSpots { get; set; }

    /// <summary>Whether treasure the game has loaded near the player is marked on the map, anywhere.</summary>
    public bool ShowNearbyTreasure { get; set; }
}
