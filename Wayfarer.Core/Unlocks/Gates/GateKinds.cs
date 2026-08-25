namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>The kind strings, in one place, so the data schema and the evaluators cannot spell
/// them differently. These are the names of <i>kinds of requirement</i>, never the names of
/// catalogue entries — nothing here recognises a quest, a mount or a duty.</summary>
public static class GateKinds
{
    public const string AllOf = "allOf";
    public const string AnyOf = "anyOf";
    public const string QuestComplete = "questComplete";
    public const string QuestAnyOf = "questAnyOf";
    public const string DutyUnlocked = "dutyUnlocked";
    public const string DutyComplete = "dutyComplete";
    public const string MountOwned = "mountOwned";
    public const string MinionOwned = "minionOwned";
    public const string ItemHeld = "itemHeld";
    public const string CharacterLevelAtLeast = "characterLevelAtLeast";
    public const string JobLevelAtLeast = "jobLevelAtLeast";
    public const string TribeRankAtLeast = "tribeRankAtLeast";
    public const string GrandCompanyRankAtLeast = "grandCompanyRankAtLeast";
    public const string AchievementComplete = "achievementComplete";
    public const string AetherCurrentsComplete = "aetherCurrentsComplete";
    public const string SharedFateRankAtLeast = "sharedFateRankAtLeast";
    public const string ZoneProgressAtLeast = "zoneProgressAtLeast";
    public const string Unverifiable = "unverifiable";

    /// <summary><c>scope</c> values for <see cref="DutyUnlocked"/> / <see cref="DutyComplete"/>.</summary>
    public const string ScopeInstance = "instance";

    /// <inheritdoc cref="ScopeInstance"/>
    public const string ScopePublic = "public";

    /// <summary><c>scope</c> values for <see cref="ItemHeld"/>.</summary>
    public const string ScopeAny = "any";

    /// <inheritdoc cref="ScopeAny"/>
    public const string ScopeKeyItem = "keyItem";

    /// <inheritdoc cref="ScopeAny"/>
    public const string ScopeSaddlebag = "saddlebag";

    /// <summary><c>scope</c> values for <see cref="ZoneProgressAtLeast"/>.</summary>
    public const string ScopeEureka = "eureka";

    /// <inheritdoc cref="ScopeEureka"/>
    public const string ScopeBozja = "bozja";

    /// <summary>Every kind the shipped registry implements. Used by the dataset tests to prove the
    /// data and the code cannot drift apart, and by nothing that makes a decision.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        AllOf, AnyOf, QuestComplete, QuestAnyOf, DutyUnlocked, DutyComplete, MountOwned,
        MinionOwned, ItemHeld, CharacterLevelAtLeast, JobLevelAtLeast, TribeRankAtLeast,
        GrandCompanyRankAtLeast, AchievementComplete, AetherCurrentsComplete,
        SharedFateRankAtLeast, ZoneProgressAtLeast, Unverifiable,
    ];
}
