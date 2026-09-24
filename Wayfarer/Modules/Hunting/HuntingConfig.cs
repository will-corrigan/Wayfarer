namespace Wayfarer.Modules.Hunting;

/// <summary>The hunting module's own settings. Saved as <c>hunting.json</c>.</summary>
internal sealed class HuntingConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Whether Wayfarer puts a Follow button on the Hunting Log's pages.</summary>
    public bool FollowFromLog { get; set; } = true;

    /// <summary>Whether Wayfarer puts a Follow button on the mark bills.</summary>
    public bool FollowFromBills { get; set; } = true;

    /// <summary>The hunt each character is following, by the character's content id. A player with
    /// several characters is following something different on each.</summary>
    public Dictionary<ulong, Hunt> Followed { get; set; } = [];
}
