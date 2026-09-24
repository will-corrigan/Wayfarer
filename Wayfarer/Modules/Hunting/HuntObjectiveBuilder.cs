using Wayfarer.Guidance;
using Wayfarer.Routing;
using Wayfarer.World;

namespace Wayfarer.Modules.Hunting;

/// <summary>Turns a hunt's targets into the objective the app guides to: the first one not yet
/// done, in the order the game lists them.
///
/// <para>A hunting log page and a mark bill are both done in order, one monster at a time, and
/// end when the last is done. So the objective names only the monster being hunted now: naming
/// them all would let the guide pick whichever is nearest, which is not the order.</para>
///
/// <para>Where to go is, in turn: the monster itself when one is standing in sight; the duty it
/// lives in, to queue for, when it lives nowhere a player can walk; the FATE it appears in, when
/// that FATE is up; and otherwise the part of the map the game names for it.</para></summary>
internal static class HuntObjectiveBuilder
{
    /// <summary>The objective for a hunt, or null when every target is done.</summary>
    /// <param name="headline">What the hunt is called: the log page or the bill.</param>
    /// <param name="kind">What sort of hunt it is, as the guide heads it.</param>
    /// <param name="quarries">The hunt's targets in the order the game lists them.</param>
    /// <param name="seen">The nearest monster standing in sight with this name id, or null.</param>
    /// <param name="fateAt">Where this FATE is being fought right now, or null when it is not up
    /// or cannot be known.</param>
    /// <param name="fateKnown">Whether the game can say if this FATE is up: only for the zone the
    /// player is standing in. Anywhere else, not up is not something to tell them.</param>
    public static Objective? Build(
        string headline,
        string kind,
        IReadOnlyList<Quarry> quarries,
        Func<uint, Found?> seen,
        Func<uint, Place?> fateAt,
        Func<uint, bool> fateKnown)
    {
        ArgumentNullException.ThrowIfNull(quarries);
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(fateAt);
        ArgumentNullException.ThrowIfNull(fateKnown);

        if (quarries.FirstOrDefault(quarry => !quarry.Done) is not { } now)
        {
            return null;
        }

        var fate = now.Fate is { } named ? fateAt(named.Id) : null;
        Destination where = seen(now.NameId) is { } found ? new Destination.AtObject(found.Id, found.At)
            : now.Duty is { } duty ? new Destination.InDuty(duty)
            : fate is { } fighting ? new Destination.Reachable([fighting])
            : new Destination.Reachable(now.Places);

        var words = now.Fate is { } during
            ? $"{now.Name} (FATE: {during.Name}{(fate is null && fateKnown(during.Id) ? ", not up right now" : string.Empty)})"
            : now.Name;

        return new Objective(headline, [new ObjectiveEntry(words, new Progress(now.Have, now.Need), where)], Kind: kind);
    }
}
