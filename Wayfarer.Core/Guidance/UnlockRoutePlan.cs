namespace Wayfarer.Core.Guidance;

/// <summary>The unlock-route source's own semantics, kept pure and testable: when a pickup counts
/// as done, and the exact words it puts on the readout. The plugin-side source supplies the live
/// quest reads and composes these into a <see cref="GuidanceChain{T}"/>.</summary>
public static class UnlockRoutePlan
{
    /// <summary>The mode indicator shown whenever an unlock route owns the arrow. Title case, like
    /// every other heading the game draws.</summary>
    public const string SourceLabel = "Unlock Route";

    /// <summary>What this module calls itself on the readout's banner, which prints "Current" in
    /// front of it — "Current Unlock". Singular, because the banner names the one thing being
    /// tracked, and without "Route" because the banner is worn for a single pickup as well as for a
    /// multi-stop plan. See <see cref="ObjectiveCopy.SourceName"/>.</summary>
    public const string SourceName = "Unlock";

    /// <summary>A pickup leg is done once its quest has been ACCEPTED (the player walked to the
    /// giver and took it — the whole point of the route) or was already COMPLETE (the route was
    /// planned from stale data, or the player did it another way). This is the unlock source's
    /// completion signal and lives only here: nothing outside this feature knows that "accepted"
    /// means "done" for a pickup, which is precisely why nothing outside it may decide.</summary>
    public static bool IsPickedUp(bool questAccepted, bool questComplete) => questAccepted || questComplete;

    /// <summary>What goes in the readout's bar: <b>the name of the quest that grants the unlock</b>,
    /// and nothing else.
    ///
    /// <para><b>This is the rule the whole banner rests on, and it was broken.</b> The bar only ever
    /// carries a string the game itself would print — a quest's name, a monster's name — because the
    /// bar IS the game's Main Scenario Guide plate and a player reads whatever is on it as a game
    /// element. It used to carry <c>"Unlocks: Ceremony of Eternal Bonding"</c>: an invented label,
    /// prefixed with a word about our own data model, long enough that the plate cut it short, and it
    /// pushed the real quest name — "The Ties That Bind" — down into a subordinate line. Exactly
    /// backwards. That is why this is an identity function rather than a formatter: there is nowhere
    /// left for a decoration to get in.</para>
    ///
    /// <para>The catalogue binds every unlock entry to a quest row, so the name always exists. The
    /// invented phrasing moves down one level, into <see cref="Detail"/> — the slot the job-quest
    /// pattern gives us for free.</para></summary>
    public static string Headline(string questName) => questName;

    /// <summary>The subordinate line beneath the name: who to go and see, and what taking the quest
    /// gets you.
    ///
    /// <para>No <c>"Unlocks:"</c> prefix and no <c>"Pick up:"</c> prefix — both are labels about our
    /// data model rather than anything the game would write. A sentence instead, naming the giver
    /// (the actual instruction) and the reward (why the player asked). With no giver known it
    /// collapses to the reward alone.</para></summary>
    public static string Detail(string unlockName, string? giverName) =>
        giverName is { Length: > 0 } giver
            ? $"Speak with {giver} to unlock {unlockName}"
            : $"Unlocks {unlockName}";
}
