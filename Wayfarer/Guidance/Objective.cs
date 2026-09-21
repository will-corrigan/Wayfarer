namespace Wayfarer.Guidance;

/// <summary>What a module wants the player guided to right now. The one thing a module hands the
/// app: every word about the thing, and where each part of it is. The app never writes any of
/// these words; it routes the places and adds its own words about getting there.</summary>
/// <param name="Headline">What the thing is called: the quest's name, the monster's name.</param>
/// <param name="Kind">What kind of thing it is, in the words a surface would put above the name:
/// "Followed Quest", "Hunting Log". Null when the surface should keep its own heading. Only the
/// module that made the objective knows this, so only it writes it.</param>
/// <param name="Entries">The lines the game's own tracker would show for it, in order. A step with
/// one target is one entry; a step whose parts can be done in any order is several.</param>
/// <param name="HeadlinePressable">Whether pressing the headline does anything. What it does is
/// the source's own business: see <see cref="IObjectiveSource.PressHeadline"/>.</param>
public sealed record Objective(string Headline, IReadOnlyList<ObjectiveEntry> Entries, bool HeadlinePressable = false, string? Kind = null)
{
    /// <summary>The entry the app guides to: the first, in the order the module listed them, that
    /// has somewhere to go. The list reads top down the way the game wrote it, so the player is
    /// taken through it in that order rather than to whichever part happens to be nearest.
    ///
    /// <para>Somewhere to go is a place to walk to or a duty to queue for. A duty is not a point on
    /// a map and nothing routes to it, but it is still what the step is about and still something
    /// the player can be sent to do. Null when no entry is either.</para></summary>
    public ObjectiveEntry? Guided()
    {
        foreach (var entry in Entries)
        {
            if (entry.Where is Destination.Reachable or Destination.AtObject or Destination.InDuty)
            {
                return entry;
            }
        }

        return null;
    }
}
