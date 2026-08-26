namespace Wayfarer.Core.Unlocks;

/// <summary>Whether an entry has somewhere to go, stated rather than inferred.
///
/// <para><b>Why this exists.</b> Routability used to be a consequence of nullness: the route
/// planner filtered on <c>GiverTerritory != null</c>, and that field is only ever populated from a
/// quest's issuer location, so an entry with no quest was silently unroutable. Correct behaviour,
/// arrived at by accident, with nothing to show the player and nothing a test could assert. Whole
/// channels have no place at all — a title earned by defeating fifty thousand enemies is not
/// somewhere you walk to — and the difference between "we could not find the giver" and "there is
/// no giver" is exactly what the player needs to be told.</para>
///
/// <para>Absent on an entry means <see cref="UnlockPlaceKinds.QuestGiver"/>: that is what every
/// entry written before this field meant, so the default is the old behaviour rather than a new
/// claim about entries nobody has looked at.</para></summary>
/// <param name="Kind">One of <see cref="UnlockPlaceKinds"/>. A kind this build does not know is
/// treated as having no place, because inventing a location is the failure this field prevents.</param>
public sealed record UnlockPlace(string Kind)
{
    public UnlockPlace()
        : this(UnlockPlaceKinds.QuestGiver)
    {
    }
}

/// <summary>The closed set of place kinds. Shared with <c>data/validate-unlocks.mjs</c> so the
/// data and the code cannot spell them differently.</summary>
public static class UnlockPlaceKinds
{
    /// <summary>The place is wherever the entry's bound quest is issued — the
    /// <c>Quest.IssuerLocation</c> → <c>Level</c> row the route planner has always used.</summary>
    public const string QuestGiver = "questGiver";

    /// <summary>The game states no place for this at all, so no route affordance may be offered.
    /// What the entry needs is said instead, from the game's own words where it has them (see
    /// <see cref="UnlockDefinition.Obtain"/>).</summary>
    public const string None = "none";

    public static readonly IReadOnlyList<string> All = [QuestGiver, None];

    /// <summary>Whether this kind could ever resolve to a coordinate. Note what it does not say:
    /// that this entry's coordinate resolved. That is <see cref="ResolvedUnlock.Routable"/>, and
    /// it needs the live sheets to answer.</summary>
    public static bool CanHaveACoordinate(string? kind) =>
        kind is null || string.Equals(kind, QuestGiver, StringComparison.Ordinal);
}
