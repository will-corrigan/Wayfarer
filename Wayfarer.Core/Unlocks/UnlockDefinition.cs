namespace Wayfarer.Core.Unlocks;

public sealed class UnlockDefinition
{
    public int Level { get; set; }

    public string Unlock { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Quest { get; set; }

    public string? QuestKind { get; set; }

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
