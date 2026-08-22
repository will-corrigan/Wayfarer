namespace Wayfarer.Core.Hunting;

/// <summary>Pure candidate-selection predicate for live in-zone tracking, isolated from the
/// plugin's <c>IObjectTable</c> scan so the id-space decision is testable. The load-bearing
/// choice: ids are compared in <b>BNpcName</b> space — the caller must pass
/// <c>IBattleNpc.NameId</c> (via <c>ICharacter</c>), NOT <c>IGameObject.BaseId</c>/<c>DataId</c>,
/// which for a battle NPC is the <b>BNpcBase</b> row id. The dataset's
/// <see cref="HuntingMonster.BNpcNameId"/> values are BNpcName rows (verified against
/// <c>MonsterNoteTarget.BNpcName</c>), so a BaseId comparison would silently never match — or
/// worse, lock onto an unrelated mob on a coincidental row-id overlap.</summary>
public static class HuntingLiveTracking
{
    /// <summary>Whether a scanned battle NPC is a valid live-position candidate for the current
    /// hunting target: alive, targetable (spawn-animation/event clones excluded), and its
    /// BNpcName id matches the target's.</summary>
    public static bool IsCandidate(uint candidateBNpcNameId, uint targetBNpcNameId, bool isDead, bool isTargetable)
        => !isDead && isTargetable && candidateBNpcNameId == targetBNpcNameId;
}
