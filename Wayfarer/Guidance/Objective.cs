namespace Wayfarer.Guidance;

/// <summary>What a module wants the player guided to right now. The one thing a module hands the
/// app: every word about the thing, and where each part of it is. The app never writes any of
/// these words; it routes the places and adds its own words about getting there.</summary>
/// <param name="Headline">What the thing is called: the quest's name, the monster's name.</param>
/// <param name="Entries">The lines the game's own tracker would show for it, in order. A step with
/// one target is one entry; a step whose parts can be done in any order is several.</param>
/// <param name="HeadlinePressable">Whether pressing the headline does anything. What it does is
/// the source's own business: see <see cref="IObjectiveSource.PressHeadline"/>.</param>
public sealed record Objective(string Headline, IReadOnlyList<ObjectiveEntry> Entries, bool HeadlinePressable = false)
{
    /// <summary>The entry the app routes to: the first, in the order the module listed them, that
    /// has somewhere to go. The list reads top down the way the game wrote it, so the player is
    /// taken through it in that order rather than to whichever part happens to be nearest. Null
    /// when no entry can be reached.</summary>
    public ObjectiveEntry? FirstReachable()
    {
        foreach (var entry in Entries)
        {
            if (entry.Where is Destination.Reachable)
            {
                return entry;
            }
        }

        return null;
    }
}
