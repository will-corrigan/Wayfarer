namespace Wayfarer.Modules.Treasure;

/// <summary>The game's own map chests, one per metal.</summary>
internal static class TreasureIcons
{
    /// <summary>The bronze chest, also drawn faded for a spot with nothing on it.</summary>
    public const uint Bronze = 60356;

    /// <summary>The silver chest with sparkles.</summary>
    public const uint Silver = 60355;

    /// <summary>The gold chest with sparkles, also drawn for a chest whose metal is not known.</summary>
    public const uint Gold = 60354;

    /// <summary>The chest drawn for a loaded chest of this metal.</summary>
    public static uint For(TreasureTier tier) => tier switch
    {
        TreasureTier.Bronze => Bronze,
        TreasureTier.Silver => Silver,
        _ => Gold,
    };
}
