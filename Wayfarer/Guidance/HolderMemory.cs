using Dalamud.Plugin.Services;
using Wayfarer.App;
using Wayfarer.App.Config;

namespace Wayfarer.Guidance;

/// <summary>Which source each character last had guidance from, kept across sessions, so logging
/// in picks up where that character left off rather than where the last one to play did. What
/// each source was on is the source's own to remember: the hunt, the quest.</summary>
internal sealed class HolderMemory(IConfigStore configs, IPlayerState player)
{
    private const string ConfigName = "guidance";

    private readonly GuidanceConfig config = configs.Load<GuidanceConfig>(ConfigName);

    /// <summary>The name of the source this character last had guidance from, or null when nobody
    /// is logged in or this character has never been guided. Game thread only.</summary>
    public string? Last => player.LoggedIn() is { } id && config.Holders.TryGetValue(id, out var name) ? name : null;

    /// <summary>Notes who now holds guidance, for the character logged in. Nobody holding is not
    /// noted: everything switched off says nothing about what to pick up again. Game thread only.</summary>
    public void Remember(IObjectiveSource? holder)
    {
        if (holder is null
            || player.LoggedIn() is not { } id
            || (config.Holders.TryGetValue(id, out var was) && string.Equals(was, holder.Name, StringComparison.Ordinal)))
        {
            return;
        }

        config.Holders[id] = holder.Name;
        configs.Save(ConfigName, config);
    }
}
