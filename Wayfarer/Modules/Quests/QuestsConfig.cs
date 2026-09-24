namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's own settings. Saved as <c>quests.json</c>.</summary>
internal sealed class QuestsConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 2;

    /// <summary>The quest each character chose to follow instead of the main scenario, by the
    /// character's content id.</summary>
    public Dictionary<ulong, ushort> Followed { get; set; } = [];

    /// <summary>The one quest followed before quests were remembered per character, in a file of
    /// version 1. Handed to the first character to log in, then cleared.</summary>
    public ushort? FollowedQuestId { get; set; }

    /// <summary>Whether Wayfarer guides the player through the quest they are on at all. This is
    /// the module's chief work, and it is a switch like any other so that switching everything off
    /// really does switch the module off.</summary>
    public bool Guide { get; set; } = true;

    /// <summary>Whether Wayfarer puts its own button in the quest journal.</summary>
    public bool FollowFromJournal { get; set; } = true;

    /// <summary>Whether Wayfarer marks the Duty Finder's rows for duties a quest leads to.</summary>
    public bool MarkDutyFinder { get; set; } = true;
}
