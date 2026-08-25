namespace Wayfarer.Core.Unlocks.Live;

/// <summary>The only code that talks to <see cref="ObservationStore"/>.
///
/// <para>Wraps a raw <see cref="IMonotonicSource{TId}"/> so the result is, from the evaluator's
/// side, indistinguishable from any other <c>Try</c> reader: evaluators never see this type and
/// never learn the store exists. That is what keeps remembering a value from becoming a per-kind
/// special case in the gate model.</para></summary>
/// <typeparam name="TId">What identifies one observable value.</typeparam>
public sealed class ObservedFloor<TId>(
    IMonotonicSource<TId> source,
    ObservationStore store,
    string kind,
    Func<TId, uint> idKey,
    Func<DateTimeOffset> clock)
{
    /// <summary>Whether the value is at least <paramref name="atLeast"/>, answered from the live
    /// read when there is one and from the remembered floor otherwise.
    ///
    /// <para><b>The threshold is here, not in the caller, and that is the point.</b> A remembered
    /// floor can prove a requirement IS met — the value was that high once and cannot have fallen.
    /// It can never prove one is NOT met: it is a lower bound, and the value may have risen since
    /// it was recorded. So a floor at or above the threshold answers, a floor below it does not,
    /// and the gate reports "cannot tell" rather than sending a player back to content they have
    /// already finished. Handing the caller a bare number instead would make the second case look
    /// exactly like the first.</para></summary>
    public bool TryAtLeast(string characterKey, TId id, int atLeast, out bool met)
    {
        if (source.TryReadLive(id, out var live))
        {
            store.Observe(characterKey, kind, idKey(id), live, clock());
            met = live >= atLeast;
            return true;
        }

        met = store.TryFloor(characterKey, kind, idKey(id), out var floor) && floor >= atLeast;
        return met;
    }
}
