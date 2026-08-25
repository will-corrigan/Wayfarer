namespace Wayfarer.Core.Navigation;

/// <summary>One aether current, as a stop on a route: what it is, how it is obtained and where the
/// player has to be.
///
/// <para>Deliberately a snapshot with no attunement flag on it. Whether a current is attuned is a
/// live bit read that is free at any moment (see <see cref="Guidance.AetherCurrentPlan"/>), so
/// baking it in would mean carrying a copy of something always cheaper to re-read — the same reason
/// a hunting leg carries no kill count.</para></summary>
/// <param name="CurrentRowId">The <c>AetherCurrent</c> sheet row id. Also the identity the client's
/// own attunement bitfield is indexed by, so it is both our key and the game's.</param>
/// <param name="CompFlgSetId">The <c>AetherCurrentCompFlgSet</c> row this current belongs to — the
/// game's own unit of "one zone's worth of currents".</param>
/// <param name="ZoneName">The <c>PlaceName</c> of the set's territory, for the readout's mode label.
/// The zone the CURRENT belongs to, not the zone its giver stands in: nine of the quest-granted
/// currents are handed out in a neighbouring city, and the route says which zone's currents it is
/// working through.</param>
/// <param name="Territory">Where the player must physically go — for a quest current this is the
/// GIVER's territory, which is why it can differ from the set's.</param>
/// <param name="QuestRowId">0 for an <see cref="AetherCurrentKind.Attunable"/> current. For a quest
/// current it is the granting quest, and it is what tells us the stop is done: once the quest is in
/// hand there is nothing left at the giver's feet.</param>
public sealed record AetherCurrentPoint(
    uint CurrentRowId,
    AetherCurrentKind Kind,
    uint CompFlgSetId,
    string ZoneName,
    uint Territory,
    uint MapId,
    float X,
    float Y,
    float Z,
    uint QuestRowId = 0,
    string? QuestName = null,
    string? GiverName = null)
{
    /// <summary>Whether this stop has a position worth routing to. Every current in the shipped
    /// sheets resolves to one — all 152 placed currents have a <c>Level</c> row and all 151
    /// quest-granted ones have an issuer location — but a patch could add one that does not, and a
    /// stop with no location must say so rather than point at the map's origin. A territory of 0 is
    /// how the sheets themselves spell "nowhere", so it is the test.</summary>
    public bool HasLocation => Territory != 0;
}
