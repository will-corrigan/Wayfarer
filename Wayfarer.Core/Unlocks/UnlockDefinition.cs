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
    /// quest name resolves to exactly one live Quest row, so two independent sources agree on
    /// what unlocks this and the game supplies the gates), <c>single-source</c> (only one source
    /// establishes the link — derived from game data alone, or the game's row is ambiguous), or
    /// <c>unverified</c> (nothing in the game's data backs this entry; its requirements are not
    /// checkable and it must never be reported as available).</summary>
    public string Confidence { get; set; } = "unverified";

    /// <summary>What was consulted, so the plugin can say what it actually knows rather than
    /// asserting. Wiki entries read <c>gamerescape:progression-guide</c>; game-data reads name
    /// the sheet and row, e.g. <c>game-data:Quest#66750</c>.</summary>
    public List<string> Sources { get; set; } = [];
}
