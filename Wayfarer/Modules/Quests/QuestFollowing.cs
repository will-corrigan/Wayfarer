using Wayfarer.App.Config;

namespace Wayfarer.Modules.Quests;

/// <summary>Which quest the player has chosen to follow instead of the main scenario. Chosen from
/// the quest journal, remembered across sessions, and let go of when the quest is no longer
/// accepted, which is how completing or abandoning it hands guidance back to the main scenario.</summary>
internal sealed class QuestFollowing(IConfigStore configs)
{
    private const string ConfigName = "quests";

    private readonly QuestsConfig config = configs.Load<QuestsConfig>(ConfigName);

    /// <summary>Raised after the followed quest changed, on whatever thread changed it.</summary>
    public event EventHandler? OnChanged;

    /// <summary>The followed quest's id, or null while the main scenario is followed.</summary>
    public ushort? Followed => config.FollowedQuestId;

    /// <summary>Whether a quest is the followed one.</summary>
    public bool IsFollowing(ushort questId) => config.FollowedQuestId == questId;

    /// <summary>Follows a quest, replacing whatever was followed before.</summary>
    public void Follow(ushort questId) => Set(questId);

    /// <summary>Back to the main scenario.</summary>
    public void Unfollow() => Set(null);

    private void Set(ushort? questId)
    {
        if (config.FollowedQuestId == questId)
        {
            return;
        }

        config.FollowedQuestId = questId;
        configs.Save(ConfigName, config);
        OnChanged?.Invoke(this, EventArgs.Empty);
    }
}
