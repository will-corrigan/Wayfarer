namespace Wayfarer.Core.Navigation;

/// <summary>One zone's worth of aether currents, as the game itself groups them: an
/// <c>AetherCurrentCompFlgSet</c> row, the territory it belongs to, and the currents it lists.
///
/// <para>The set row is the identity on purpose rather than the territory. It is what the client's
/// own zone-complete predicate takes, it is what its 31-bit completion bitfield is indexed by, and it
/// is the only place a zone's requirement is written down — so making it the identity keeps our
/// question and the game's answer about the same thing.</para></summary>
/// <param name="Points">Every current in the set, in the set row's own order, with the empty slots
/// dropped. The COUNT is the zone's requirement — see
/// <see cref="Guidance.AetherCurrentPlan.Tally"/> for why the array's LENGTH is not.</param>
public sealed record AetherCurrentZone(
    uint CompFlgSetId, uint Territory, string ZoneName, IReadOnlyList<AetherCurrentPoint> Points);
