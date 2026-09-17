using Dalamud.Plugin.Services;

namespace Wayfarer.Windows;

/// <summary>What the two pressable lines under the game's banner do. Both read the live snapshot
/// at the moment of the press rather than anything captured when the line was drawn, and both are
/// the same calls every other surface makes, through the one place each is made.</summary>
internal sealed class GuidancePresses(
    ReadoutFeed feed,
    QuestHelperConfig cfg,
    IClientState clientState,
    IPluginLog log)
{
    /// <summary>Casts the teleport the route line recommends. The only server-affecting action the
    /// plugin ever takes, and it is gated on the same setting that decides whether the line is
    /// offered at all.</summary>
    public void Teleport()
    {
        if (feed.Navigator.Current.AetheryteId is { } aetheryteId)
        {
            TeleportAction.Execute(aetheryteId, cfg, clientState, log);
        }
    }

    /// <summary>Opens the game's own Duty Finder at the duty the objective is inside. The id is null
    /// for a duty the player has not unlocked, and the composer gives that line no action — so this
    /// is the second guard on a press that should never have been offered, not the first.</summary>
    public void OpenDutyFinder()
    {
        if (feed.Navigator.Current.DutyContentFinderConditionId is { } cfcId)
        {
            DutyFinderAction.Execute(cfcId);
        }
    }
}
