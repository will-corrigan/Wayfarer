using Dalamud.Plugin.Services;
using Wayfarer.App.Config;

namespace Wayfarer.Modules.Hunting;

/// <summary>Which hunt the player chose to follow, remembered per character and across sessions,
/// and the switches for where Follow is offered.
///
/// <para>Following is let go of when the hunt is done or the player presses Unfollow; either way
/// guidance goes back to whatever it was guiding before.</para></summary>
internal sealed class HuntFollowing(IConfigStore configs, IPlayerState player)
{
    private const string ConfigName = "hunting";

    private readonly HuntingConfig config = configs.Load<HuntingConfig>(ConfigName);

    /// <summary>The hunt this character is following, or null. Null too while nobody is logged in.</summary>
    public Hunt? Followed => Character() is { } id && config.Followed.TryGetValue(id, out var hunt) ? hunt : null;

    /// <summary>Whether the Hunting Log's pages get a Follow button.</summary>
    public bool FromLog
    {
        get => config.FollowFromLog;
        set
        {
            if (config.FollowFromLog != value)
            {
                config.FollowFromLog = value;
                configs.Save(ConfigName, config);
            }
        }
    }

    /// <summary>Whether mark bills get a Follow button.</summary>
    public bool FromBills
    {
        get => config.FollowFromBills;
        set
        {
            if (config.FollowFromBills != value)
            {
                config.FollowFromBills = value;
                configs.Save(ConfigName, config);
            }
        }
    }

    /// <summary>Whether this is the hunt being followed.</summary>
    public bool IsFollowing(Hunt hunt) => Followed == hunt;

    /// <summary>Follows a hunt, replacing whatever this character followed before. Does nothing
    /// while nobody is logged in, since there is nobody to remember it for.</summary>
    public void Follow(Hunt hunt)
    {
        if (Character() is not { } id || (config.Followed.TryGetValue(id, out var was) && was == hunt))
        {
            return;
        }

        config.Followed[id] = hunt;
        configs.Save(ConfigName, config);
    }

    /// <summary>Stops following, for this character.</summary>
    public void Unfollow()
    {
        if (Character() is { } id && config.Followed.Remove(id))
        {
            configs.Save(ConfigName, config);
        }
    }

    private ulong? Character() => player.ContentId is var id and not 0 ? id : null;
}
