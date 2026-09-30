using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Wayfarer.Modules.Quests;

/// <summary>Whether the player is on a ride a quest handed them: an amaro, a kongamato, Whei Ahf,
/// magitek armor, or a transformation. Their own mounts do not count, which is what keeps a player
/// riding their chocobo to a "speak with" line pointed at the person. Game thread only.</summary>
internal sealed class QuestRide(IObjectTable objects, IUnlockState unlocks, ICondition condition)
{
    /// <summary>Whether the player is on a quest's ride right now: mounted on something they do not
    /// own, transformed, or piloting a machine.</summary>
    public bool Now()
    {
        // A passenger on someone else's mount is on a mount they do not own, but not on a quest's ride.
        if (condition[ConditionFlag.RidingPillion])
        {
            return false;
        }

        if (condition[ConditionFlag.Transformed] || condition[ConditionFlag.PilotingMech])
        {
            return true;
        }

        return objects.LocalPlayer?.CurrentMount is { } mount
            && mount.ValueNullable is { } row
            && !unlocks.IsMountUnlocked(row);
    }
}
