using Wayfarer.Core.Hunting;

namespace Wayfarer;

/// <summary>One resolved hunting-log target ready for display/navigation — the plugin-side view
/// over a <see cref="HuntingMonster"/> once its curated coordinate has been converted to world
/// space (or, once in its zone, overridden by a live <c>IObjectTable</c> position — see
/// <see cref="IsLivePosition"/>) and its display name resolved from the <c>BNpcName</c> sheet.
/// Duty-gated (non-routable) targets carry <see cref="DutyName"/>/<see cref="DutyContentFinderConditionId"/>
/// instead of usable world coordinates.
///
/// <para><see cref="ZoneName"/> is the target's own territory, not the player's: a hunting row that
/// says only "0 / 3" cannot be told apart from the row above it, and the zone is the fact that
/// makes it a place you can go rather than a name you have to look up.</para></summary>
internal sealed record HuntingTargetView(
    HuntingMonster Monster,
    string MonsterName,
    int Killed,
    int Required,
    uint TerritoryTypeId,
    uint MapId,
    float WorldX,
    float WorldY,
    float WorldZ,
    bool IsLivePosition,
    string? DutyName,
    uint? DutyContentFinderConditionId,
    string? ZoneName = null)
{
    /// <summary>True when this target has a usable world position to route/arrow toward — false
    /// only for the Grand-Company-Elite duty-gated records; every other target is text-only.</summary>
    public bool IsRoutable => DutyName is null;
}
