namespace Wayfarer.Guidance;

/// <summary>What guidance remembers between sessions. Saved as <c>guidance.json</c>.</summary>
internal sealed class GuidanceConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The source each character last had guidance from, by the character's content id,
    /// named the way the source names itself.</summary>
    public Dictionary<ulong, string> Holders { get; set; } = [];
}
