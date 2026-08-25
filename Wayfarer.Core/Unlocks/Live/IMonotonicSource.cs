namespace Wayfarer.Core.Unlocks.Live;

/// <summary>A reader for a value that (a) is not always live-readable and (b) is, once observed,
/// provably non-decreasing.
///
/// <para><b>Implementing this interface IS the declaration.</b> There is no separate flag to set
/// or forget, because a reader with no such proof has nothing to implement against. Implementing
/// it for a value that can decrease is the one way to make the whole remembered-floor mechanism
/// unsound: a character who reached a rank, lost it, and is then asked whether they meet a
/// requirement would be told yes.</para>
///
/// <para>Do <b>not</b> implement this for Eureka's elemental level. It can decrease — from level
/// 11 onward a death not raised in time can cost more experience than the current level holds and
/// the character delevels. Bozja's resistance rank, Shared FATE rank and achievement completion
/// are all monotonic and are the values this exists for.</para></summary>
/// <typeparam name="TId">What identifies one observable value — a zone, an achievement.</typeparam>
public interface IMonotonicSource<in TId>
{
    /// <summary>True, with <paramref name="value"/> set, when the value is currently
    /// authoritative: in the right zone, or after a server round trip. Must never itself trigger
    /// that round trip. False otherwise, and the caller falls back to the remembered floor.</summary>
    bool TryReadLive(TId id, out int value);
}
