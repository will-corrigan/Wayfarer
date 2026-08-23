using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer;

/// <summary>Opens the game's own Duty Finder queue confirmation for a specific duty — client UI
/// navigation, not a server-affecting action (see <see cref="TeleportAction"/>'s doc comment for
/// that distinction). One place for the one call every duty-gated "Go" affordance makes
/// (<see cref="Windows.NativeHubWindow"/>'s Hunting Log rows, <see cref="Windows.HuntingWindow"/>'s
/// duty link, <see cref="Windows.ArrowWindow"/>'s guided-quest-in-a-duty line and the game's own
/// context menu), so it is made exactly once rather than reimplemented at each call site.</summary>
internal static unsafe class DutyFinderAction
{
    public static void Execute(uint contentFinderConditionId)
    {
        var agent = AgentContentsFinder.Instance();
        if (agent != null)
        {
            agent->OpenRegularDuty(contentFinderConditionId, false);
        }
    }
}
