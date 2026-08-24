namespace Wayfarer.Core.Unlocks;

/// <summary>One row of <c>data/unlocks-by-level.json</c> as the plugin reads it: what the unlock
/// is, what opens it, and how well corroborated that claim is.
///
/// <para>This is only the half of the entry the plugin acts on. The file also carries editorial
/// fields — <c>questKind</c>, and the dataset's own header — which are there for whoever maintains
/// the catalogue and are checked by <c>data/validate-unlocks.mjs</c>. They are deliberately absent
/// here: a property the plugin never reads reads as a promise the plugin never keeps.</para></summary>
public sealed class UnlockDefinition
{
    /// <summary>The level a source states for this unlock, or <c>null</c> when no source states
    /// one at all.
    ///
    /// <para>Null is not "level 0" and not "level 1". Five sections of the source guide carry no
    /// level, and the original import quietly filled them in with the previous expansion's cap —
    /// putting 13 entries at a number nobody had ever said. The trophy mounts are the case that
    /// cannot be fixed by looking harder: the guide gives no level and the quest that grants them
    /// is a hidden level-1 reward row, so their real requirement is owning a set of Extreme-trial
    /// mounts and there is no level to print. Those entries carry a <see cref="Category"/>
    /// instead and belong in their own section, not sorted among low-level content.</para></summary>
    public int? Level { get; set; }

    /// <summary>Where <see cref="Level"/> came from — the guide section that states it, or the
    /// Quest row whose accept level it is. Required whenever a level is present, so that an
    /// invented one cannot be committed without a source to point at.</summary>
    public string? LevelSource { get; set; }

    /// <summary>What this entry is, for the entries that have no <see cref="Level"/>. Taken from
    /// the source's own section heading rather than invented ("Heavensward Unique Quest
    /// Rewards"). Required when there is no level, and only then.</summary>
    public string? Category { get; set; }

    public string Unlock { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Quest { get; set; }

    /// <summary>Quest sheet row ids, any ONE of which completes this unlock — the Grand Company,
    /// starting-city and relic-weapon variants, where a character does exactly one of the set.
    ///
    /// <para>Row ids rather than a name, because the name is precisely what was ambiguous: the
    /// game ships three quests called <c>The Company You Keep</c> and matching on the string
    /// picked one arbitrarily, which told two thirds of characters they had not done something
    /// they had. Empty for the ordinary case of one quest, one unlock.</para></summary>
    public List<uint> QuestAnyOf { get; set; } = [];

    /// <summary>The sheet identity this entry actually grants, or <c>null</c> when the game has no
    /// row for it.
    ///
    /// <para>The catalogue used to know only what an unlock is <i>called</i>, and a name cannot be
    /// drawn: the picture of a mount lives on <c>Mount.Icon</c>, and "Firebird (Mount)" is a
    /// sentence in a guide rather than a row in a sheet. This is the pair a lookup can start from —
    /// see <see cref="UnlockReward"/>.</para>
    ///
    /// <para>Null for the many <c>system</c> entries that open a feature the game keeps no row for.
    /// That is an answer, not a gap, and nothing downstream may present it as one.</para></summary>
    public UnlockReward? Reward { get; set; }

    public string? Notes { get; set; }

    public string? Description { get; set; }

    public string Priority { get; set; } = "nice";

    public bool Cosmetic { get; set; }

    /// <summary>Requirements the game keeps somewhere a plugin can't read — see
    /// <see cref="UnlockRequirement"/>. Null for an entry the Quest sheet fully describes.</summary>
    public UnlockRequirement? Requires { get; set; }

    /// <summary>How well corroborated this entry's claim is: <c>verified</c> (the catalogue's
    /// quest name resolves to exactly one live Quest row and nothing contradicts the pairing),
    /// <c>single-source</c> (only one source establishes the link — derived from game data alone,
    /// the game's row is ambiguous, or another entry cites the same row at a level far enough away
    /// that both cannot be right), or <c>unverified</c> (nothing in the game's data backs this
    /// entry; its requirements are not checkable and it must never be reported as available).
    ///
    /// <para>Note what <c>verified</c> does <b>not</b> mean. A name resolving to a live row shows
    /// that the quest exists, not that it is the quest that unlocks this — those are different
    /// claims, and only the first is checkable from inside the plugin. The strongest contrary
    /// evidence the catalogue can produce is two entries naming the same row at different levels;
    /// <c>data/validate-unlocks.mjs</c> refuses to let either of those call itself verified. What
    /// is left is a well-corroborated guess, and the gates it drives are read from the game's own
    /// sheet rather than from this field.</para></summary>
    public string Confidence { get; set; } = "unverified";

    /// <summary>What was consulted, so the plugin can say what it actually knows rather than
    /// asserting. Wiki entries read <c>gamerescape:progression-guide</c>; game-data reads name
    /// the sheet and row, e.g. <c>game-data:Quest#66750</c>.</summary>
    public List<string> Sources { get; set; } = [];
}
