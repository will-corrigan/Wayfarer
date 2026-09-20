namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's own settings. Saved as <c>quests.json</c>.</summary>
internal sealed class QuestsConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The quest the player chose to follow instead of the main scenario, or null.</summary>
    public ushort? FollowedQuestId { get; set; }

    /// <summary>Whether Wayfarer puts its own button in the quest journal.</summary>
    public bool FollowFromJournal { get; set; } = true;

    /// <summary>Whether Wayfarer marks the Duty Finder's rows for duties a journal quest leads to.</summary>
    public bool MarkDutyFinder { get; set; } = true;
}
