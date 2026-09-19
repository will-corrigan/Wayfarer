namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's own settings. Saved as <c>quests.json</c>.</summary>
internal sealed class QuestsConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The quest the player chose to follow instead of the main scenario, or null.</summary>
    public ushort? FollowedQuestId { get; set; }
}
