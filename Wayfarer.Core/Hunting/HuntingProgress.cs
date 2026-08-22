namespace Wayfarer.Core.Hunting;

/// <summary>Pure hunting-log progress logic: page tri-state and remaining-target selection over
/// injected data/reader delegates. No Dalamud dependency — the plugin-side reader is responsible
/// for turning <c>MonsterNoteManager</c> reads into the primitives these take.</summary>
public static class HuntingProgress
{
    /// <summary>Tri-state for a 1-based rank page relative to the player's current rank on that
    /// log (also 1-based, e.g. from the live <c>MonsterNoteManager</c> rank-progress read).</summary>
    public static HuntingPageState PageState(int rank, int currentRank)
    {
        if (rank < currentRank)
        {
            return HuntingPageState.Done;
        }

        return rank == currentRank ? HuntingPageState.Current : HuntingPageState.Locked;
    }

    /// <summary>Monsters on the current page's tasks that are not yet fully killed, in the
    /// dataset's positional order (task, then monster within task) — the same order live
    /// progress is read by, so callers can zip this 1:1 against a live count read if needed.
    /// Only meaningful for a page whose <see cref="PageState"/> is <see cref="HuntingPageState.Current"/>
    /// — earlier pages are already Done (nothing remaining) and later pages are Locked (no live
    /// counts exist yet to judge "remaining" by).</summary>
    /// <param name="currentPageRank">The <see cref="HuntingRank"/> for the player's current rank
    /// page.</param>
    /// <param name="killedCount">Live kill count for (taskIndex, monsterIndex), injected so this
    /// stays pure/testable — the plugin-side reader turns this into a
    /// <c>MonsterNoteManager</c> <c>Counts[]</c> read.</param>
    public static List<HuntingMonster> RemainingForCurrentPage(
        HuntingRank currentPageRank,
        Func<int, int, int> killedCount)
    {
        var remaining = new List<HuntingMonster>();
        foreach (var task in currentPageRank.Tasks)
        {
            foreach (var monster in task.Monsters)
            {
                if (killedCount(task.TaskIndex, monster.MonsterIndex) < monster.RequiredKills)
                {
                    remaining.Add(monster);
                }
            }
        }

        return remaining;
    }
}
