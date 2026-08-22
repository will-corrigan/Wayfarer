using Wayfarer.Core.Hunting;

namespace Wayfarer;

/// <summary>One resolved hunting-log target ready for display/navigation — the plugin-side view
/// over a <see cref="HuntingMonster"/> once its curated coordinate has been converted to world
/// space (or, once in its zone, overridden by a live <c>IObjectTable</c> position — see
/// <see cref="IsLivePosition"/>) and its display name resolved from the <c>BNpcName</c> sheet.
/// Duty-gated (non-routable) targets carry <see cref="DutyName"/>/<see cref="DutyContentFinderConditionId"/>
/// instead of usable world coordinates.</summary>
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
    uint? DutyContentFinderConditionId)
{
    /// <summary>True when this target has a usable world position to route/arrow toward — false
    /// only for the Grand-Company-Elite duty-gated records (spec §5's "otherwise text-only").</summary>
    public bool IsRoutable => DutyName is null;
}
