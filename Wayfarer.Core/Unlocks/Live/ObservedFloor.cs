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
    /// <summary>The live value when it can be read, otherwise the highest ever observed for this
    /// character. False only when neither is available — the honest "never observed".</summary>
    public bool TryValue(string characterKey, TId id, out int value)
    {
        if (source.TryReadLive(id, out value))
        {
            store.Observe(characterKey, kind, idKey(id), value, clock());
            return true;
        }

        return store.TryFloor(characterKey, kind, idKey(id), out value);
    }
}
