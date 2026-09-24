using Dalamud.Plugin.Services;
using Wayfarer.App;
using Wayfarer.App.Config;

namespace Wayfarer.Modules.Quests;

/// <summary>Which quest the player has chosen to follow instead of the main scenario. Chosen from
/// the quest journal, remembered per character and across sessions, and let go of when the quest
/// is no longer accepted, which is how completing or abandoning it hands guidance back to the main
/// scenario.</summary>
internal sealed class QuestFollowing(IConfigStore configs, IPlayerState player)
{
    private const string ConfigName = "quests";

    private readonly QuestsConfig config = configs.Load<QuestsConfig>(ConfigName);

    /// <summary>The quest this character is following, or null while the main scenario is followed
    /// or nobody is logged in. Game thread only.</summary>
    public ushort? Followed => Character() is { } id && config.Followed.TryGetValue(id, out var quest) ? quest : null;

    /// <summary>Whether Wayfarer guides the player through their quests at all.</summary>
    public bool Guiding
    {
        get => config.Guide;
        set
        {
            if (config.Guide != value)
            {
                config.Guide = value;
                configs.Save(ConfigName, config);
            }
        }
    }

    /// <summary>Whether Wayfarer puts its own button in the quest journal. Switching it off leaves
    /// whatever is already followed followed; there is just no longer a way to change it there.</summary>
    public bool FromJournal
    {
        get => config.FollowFromJournal;
        set
        {
            if (config.FollowFromJournal != value)
            {
                config.FollowFromJournal = value;
                configs.Save(ConfigName, config);
            }
        }
    }

    /// <summary>Whether Wayfarer marks the Duty Finder's rows for duties an accepted quest still
    /// leads to.</summary>
    public bool MarkDuties
    {
        get => config.MarkDutyFinder;
        set
        {
            if (config.MarkDutyFinder != value)
            {
                config.MarkDutyFinder = value;
                configs.Save(ConfigName, config);
            }
        }
    }

    /// <summary>Whether a quest is the one this character follows. Game thread only.</summary>
    public bool IsFollowing(ushort questId) => Followed == questId;

    /// <summary>Follows a quest for this character, replacing whatever they followed before. Does
    /// nothing while nobody is logged in, since there is nobody to remember it for.</summary>
    public void Follow(ushort questId) => Set(questId);

    /// <summary>Back to the main scenario, for this character.</summary>
    public void Unfollow() => Set(null);

    private void Set(ushort? questId)
    {
        if (Character() is not { } id || Followed == questId)
        {
            return;
        }

        if (questId is { } quest)
        {
            config.Followed[id] = quest;
        }
        else
        {
            config.Followed.Remove(id);
        }

        configs.Save(ConfigName, config);
    }

    /// <summary>The character logged in, or null. The first to ask is handed the quest a version 1
    /// file followed for everyone, which was theirs as much as anyone's.</summary>
    private ulong? Character()
    {
        if (player.LoggedIn() is not { } id)
        {
            return null;
        }

        if (config.FollowedQuestId is { } before)
        {
            config.Followed.TryAdd(id, before);
            config.FollowedQuestId = null;
            config.Version = 2;
            configs.Save(ConfigName, config);
        }

        return id;
    }
}
