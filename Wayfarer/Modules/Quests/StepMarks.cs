using Wayfarer.World;

namespace Wayfarer.Modules.Quests;

/// <summary>Which of the things a quest names belong to the step the player is on.
///
/// <para>A quest names every object it ever uses among its parameters, and a step that sends the
/// player into ruins to find one statue stands it among others the quest also names: earlier
/// statues, and statues put there to mislead. "Put to the Proof" asks for the wolf statue, and the
/// colibri statue beside it is a decoy; being nearer, the colibri won the search. The quest's
/// listeners say which objects each step uses, and how: one listed for the step alone is the
/// step's own, one listed as lasting from then on is part of the scenery it sets. Where a step
/// lists objects of its own, things it only lists as lasting are left out; things it does not list
/// at all are left out too. A quest that lists no objects for the step is taken as it was.</para></summary>
internal static class StepMarks
{
    /// <summary>A listener's step value meaning "from then on, for good".</summary>
    private const byte Lasting = 255;

    /// <summary>The marks a step is about, from everything the quest names.</summary>
    /// <param name="marks">Everything the quest names.</param>
    /// <param name="listeners">The quest's listeners: who or what, the step each is listed for, and
    /// the step it is listed until (<see cref="Lasting"/> for good).</param>
    /// <param name="sequence">The step the player is on.</param>
    /// <param name="nameOf">What an object is called, empty when it has no name.</param>
    public static IReadOnlyList<Mark> For(IReadOnlyList<Mark> marks, IEnumerable<(uint Listener, byte Spawn, byte Despawn)> listeners, byte sequence, Func<uint, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(nameOf);

        var things = marks.Where(mark => mark.Kind == MarkKind.Thing).Select(mark => mark.Id).ToHashSet();
        var thisStep = listeners.Where(listener => listener.Spawn == sequence && things.Contains(listener.Listener)).ToList();
        var own = thisStep.Where(listener => listener.Despawn != Lasting).Select(listener => listener.Listener).ToHashSet();
        if (own.Count == 0)
        {
            return marks;
        }

        // Only something unlike everything the step keeps is left out. Things a step uses often stay
        // in the world once used, and a fourth solid rock beside three is one of them, not a decoy;
        // which of several alike things is still wanted is settled by watching the player.
        var ownNames = own.Select(nameOf).Where(name => name.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = thisStep
            .Select(listener => listener.Listener)
            .Where(id => own.Contains(id) || ownNames.Contains(nameOf(id)))
            .ToHashSet();
        return [.. marks.Where(mark => mark.Kind != MarkKind.Thing || kept.Contains(mark.Id))];
    }
}
