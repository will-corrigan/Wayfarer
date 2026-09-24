using Wayfarer.Routing;

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
    /// <summary>What an entry in another zone is worth against one in this zone: always more, so
    /// anything underfoot is preferred whatever the numbers say. Two zones can hold the very same
    /// coordinates, and a step often names ground in a zone the player is not standing in.</summary>
    private const float AnotherZone = 1_000_000f;

    /// <summary>What an entry with nowhere to walk is worth — a duty, which is queued rather than
    /// travelled to. More than any real distance, so it is taken only when nothing else can be.</summary>
    private const float NowhereToWalk = 2_000_000f;

    /// <summary>The entry the app guides to: of those with somewhere to go, the nearest.
    ///
    /// <para>Every entry here belongs to one step of the quest — the game numbers its steps, and
    /// the lines of one step all carry the same number. A step's lines are therefore done in any
    /// order the player likes: speak to these three leaders, gather from those three merchants,
    /// slay squirrels and ladybugs and funguars. Sending the player to the first one the sheet
    /// happened to write, when another is at their feet, is walking them past the answer.</para>
    ///
    /// <para>Where the player is standing decides it. Without that — not logged in, or between
    /// zones — the order the module listed them in is all there is, and the first stands.</para>
    ///
    /// <para>Somewhere to go is a place to walk to or a duty to queue for. A duty is not a point on
    /// a map and nothing routes to it, but it is still what the step is about and still something
    /// the player can be sent to do. Null when no entry is either.</para></summary>
    /// <param name="from">Where the player stands, or null when that cannot be known.</param>
    public ObjectiveEntry? Guided(Place? from = null)
    {
        ObjectiveEntry? nearest = null;
        var shortest = float.MaxValue;

        foreach (var entry in Entries)
        {
            if (entry.Where is not (Destination.Reachable or Destination.AtObject or Destination.InDuty or Destination.InRoulette))
            {
                continue;
            }

            if (from is null)
            {
                return entry;
            }

            // Strictly nearer, so an entry only displaces one the sheet wrote earlier by actually
            // being closer. Equal distances keep the order the module gave them.
            var far = Far(entry.Where, from);
            if (far < shortest)
            {
                (shortest, nearest) = (far, entry);
            }
        }

        return nearest;
    }

    /// <summary>How far an entry is from where the player stands, for choosing between them. Not a
    /// route cost: the lines of one step are almost always within sight of each other, and routing
    /// every line of every step every frame to settle a choice between neighbours would cost far
    /// more than it could ever save.</summary>
    private static float Far(Destination where, Place from) => where switch
    {
        Destination.AtObject thing => Far(thing.At, from),
        Destination.Reachable reachable when reachable.Places.Count > 0 => reachable.Places.Min(place => Far(place, from)),
        _ => NowhereToWalk,
    };

    /// <inheritdoc cref="Far(Destination, Place)"/>
    private static float Far(Place place, Place from) =>
        place.Territory == from.Territory ? from.OnTheGround(place) : AnotherZone + from.OnTheGround(place);
}
