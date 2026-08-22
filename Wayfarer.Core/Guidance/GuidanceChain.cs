namespace Wayfarer.Core.Guidance;

/// <summary>An ordered plan over <typeparamref name="T"/> with a per-leg completion predicate.
/// Pure: no game reads, no Dalamud, no knowledge of what a leg is. Any source can compose one;
/// none has to. The arbiter knows nothing about it.
///
/// Unlock routes, a hunting rank, and any future gathering circuit or turn-in run are the same
/// shape — an ordered list of objectives, each with its own completion signal, advanced one at a
/// time — so this exists once rather than as a hand-rolled queue per source.</summary>
/// <typeparam name="T">Whatever a leg is to the owning source — a pickup, a monster, a node. The
/// chain never inspects it.</typeparam>
/// <param name="legs">The plan, in visiting order. Copied, so the caller's list is not aliased.</param>
/// <param name="isLegComplete">Evaluated against LIVE state each time <see cref="Advance"/> is
/// called — the leg objects themselves are snapshots and are never mutated.</param>
public sealed class GuidanceChain<T>(IReadOnlyList<T> legs, Func<T, bool> isLegComplete)
    where T : class
{
    private readonly List<T> legs = [.. legs];
    private int index;

    /// <summary>The leg being guided to, or null once the plan is exhausted.</summary>
    public T? Current => index < legs.Count ? legs[index] : null;

    /// <summary>1-based position of <see cref="Current"/> within the plan, for progress display.
    /// Skipped (already-complete) legs count as visited, because that is what the player sees:
    /// "stop 3 of 5" after two were already done.</summary>
    public int Index => Math.Min(index + 1, legs.Count);

    public int Total => legs.Count;

    /// <summary>Advances past every leg whose predicate now reports complete — so a chain built
    /// with already-done legs skips them immediately, and a burst of progress (three kills landing
    /// in one tick) skips several at once rather than one per tick. Returns the new
    /// <see cref="Current"/>, or null when the chain is exhausted.</summary>
    public T? Advance()
    {
        while (index < legs.Count && isLegComplete(legs[index]))
        {
            index++;
        }

        return Current;
    }

    /// <summary>Re-orders the REMAINING TAIL only; the current leg is never changed by a re-plan.
    /// Without that pin, arriving in a zone would make the arrow jump to a different target
    /// mid-approach — which reads as the guidance changing its mind about where you were
    /// going.</summary>
    public void ReplanTail(Func<IReadOnlyList<T>, IReadOnlyList<T>> reorder)
    {
        var head = index + 1;
        if (head >= legs.Count)
        {
            return;
        }

        var tail = legs.GetRange(head, legs.Count - head);
        var reordered = reorder(tail);
        legs.RemoveRange(head, legs.Count - head);
        legs.AddRange(reordered);
    }
}
