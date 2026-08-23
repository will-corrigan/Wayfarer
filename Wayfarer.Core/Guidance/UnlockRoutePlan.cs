namespace Wayfarer.Core.Guidance;

/// <summary>The unlock-route source's own semantics, kept pure and testable: when a pickup counts
/// as done, and the exact words it puts on the readout. The plugin-side source supplies the live
/// quest reads and composes these into a <see cref="GuidanceChain{T}"/>.</summary>
public static class UnlockRoutePlan
{
    /// <summary>The mode indicator shown whenever an unlock route owns the arrow. Title case, like
    /// every other heading the game draws.</summary>
    public const string SourceLabel = "Unlock Route";

    /// <summary>A pickup leg is done once its quest has been ACCEPTED (the player walked to the
    /// giver and took it — the whole point of the route) or was already COMPLETE (the route was
    /// planned from stale data, or the player did it another way). This is the unlock source's
    /// completion signal and lives only here: nothing outside this feature knows that "accepted"
    /// means "done" for a pickup, which is precisely why nothing outside it may decide.</summary>
    public static bool IsPickedUp(bool questAccepted, bool questComplete) => questAccepted || questComplete;

    public static string Headline(string unlockName) => $"Unlocks: {unlockName}";

    public static string Detail(string questName, string? giverName) =>
        giverName is { Length: > 0 } giver ? $"Pick up: {questName} from {giver}" : $"Pick up: {questName}";
}
