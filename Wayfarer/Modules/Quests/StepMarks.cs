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
/// lists objects of its own, only those are what it is about. "The Honest Truth" sends the player
/// to dig in three patches of barren soil and ties only one of them to the step, which is the one
/// pointed at first. A quest that lists no objects of its own for the step is taken as it was.</para></summary>
internal static class StepMarks
{
    /// <summary>A listener's step value meaning "from then on, for good".</summary>
    private const byte Lasting = 255;

    /// <summary>The marks a step is about, from everything the quest names.</summary>
    /// <param name="marks">Everything the quest names.</param>
    /// <param name="listeners">The quest's listeners: who or what, the step each is listed for, and
    /// the step it is listed until (<see cref="Lasting"/> for good).</param>
    /// <param name="sequence">The step the player is on.</param>
    public static IReadOnlyList<Mark> For(IReadOnlyList<Mark> marks, IEnumerable<(uint Listener, byte Spawn, byte Despawn)> listeners, byte sequence)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(listeners);

        var things = marks.Where(mark => mark.Kind == MarkKind.Thing).Select(mark => mark.Id).ToHashSet();
        var own = listeners
            .Where(listener => listener.Spawn == sequence && listener.Despawn != Lasting && things.Contains(listener.Listener))
            .Select(listener => listener.Listener)
            .ToHashSet();
        return own.Count == 0 ? marks : [.. marks.Where(mark => mark.Kind != MarkKind.Thing || own.Contains(mark.Id))];
    }
}
