namespace Wayfarer.Modules.Treasure;

/// <summary>What a chest is made of, as its model says.</summary>
internal enum TreasureTier
{
    /// <summary>Not known: not read yet, or a chest whose model names no metal.</summary>
    Unknown,

    /// <summary>A bronze chest.</summary>
    Bronze,

    /// <summary>A silver chest.</summary>
    Silver,

    /// <summary>A gold chest.</summary>
    Gold,
}
