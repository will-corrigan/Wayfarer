namespace Wayfarer.Core.Guidance;

/// <summary>Which quest the ambient source should follow this tick, and whether the player's
/// explicit override has been spent.</summary>
/// <param name="QuestId">The raw (unoffset) quest id to follow, or null when there is nothing to
/// follow at all.</param>
/// <param name="ClearOverride">True only when the override was CONFIRMED finished or abandoned —
/// never when the quest system simply could not be read.</param>
public sealed record QuestFollowOutcome(ushort? QuestId, bool ClearOverride);

/// <summary>Resolves the followed quest: the player's explicit pick if it is still accepted,
/// otherwise the head of the main scenario. Pure over injected reads, so the one behaviour that is
/// easy to get wrong — what happens when the quest system is momentarily unreadable — is pinned by
/// a test instead of by inspection.</summary>
public static class QuestFollowResolution
{
    public static QuestFollowOutcome Resolve(
        ushort? followedOverride,
        bool questManagerAvailable,
        Func<ushort, bool> isQuestAccepted,
        IReadOnlyList<ushort> mainScenarioQuestIds)
    {
        if (followedOverride is { } chosen)
        {
            // Can't confirm right now — KEEP the override rather than silently snapping the arrow
            // back to the MSQ on a frame where the quest system happened to be unreadable.
            if (!questManagerAvailable)
            {
                return new QuestFollowOutcome(chosen, ClearOverride: false);
            }

            if (isQuestAccepted(chosen))
            {
                return new QuestFollowOutcome(chosen, ClearOverride: false);
            }

            // Confirmed completed or abandoned → back to the MSQ, and forget the override.
            return new QuestFollowOutcome(MainScenarioHead(mainScenarioQuestIds), ClearOverride: true);
        }

        return new QuestFollowOutcome(MainScenarioHead(mainScenarioQuestIds), ClearOverride: false);
    }

    /// <summary>The first non-zero of the scenario tree's first three quest ids — the game's own
    /// "what's next" ordering.</summary>
    private static ushort? MainScenarioHead(IReadOnlyList<ushort> ids)
    {
        for (var i = 0; i < 3 && i < ids.Count; i++)
        {
            if (ids[i] != 0)
            {
                return ids[i];
            }
        }

        return null;
    }
}
