namespace Wayfarer.Core.Guidance;

/// <summary>Whether guidance should be shown at all right now. Deliberately separate from
/// arbitration: SUPPRESSION HIDES THE READOUT BUT NEVER RELEASES THE ENGAGEMENT TOKEN. Walk into a
/// duty mid-hunt and the readout disappears; walk out and the same objective — same
/// <see cref="ObjectiveKey"/>, same chain position — resumes. That preserves the behaviour the old
/// navigator had by accident (its pickup was a field that survived every early return) as an
/// explicit, tested rule.</summary>
public static class GuidanceSuppression
{
    public static bool ShouldHide(SuppressionInputs i) =>
        !i.LoggedIn
        || !i.PlayerPresent
        || i.InCutscene
        || i.BetweenAreas
        || (i.InCombat && i.HideInCombat)
        || (i.BoundByDuty && i.HideInDuty);
}

/// <summary>The six global display gates, as data. Two of them
/// (<paramref name="HideInCombat"/>, <paramref name="HideInDuty"/>) are user settings; the rest are
/// live game conditions.</summary>
public sealed record SuppressionInputs(
    bool LoggedIn,
    bool PlayerPresent,
    bool InCutscene,
    bool BetweenAreas,
    bool InCombat,
    bool HideInCombat,
    bool BoundByDuty,
    bool HideInDuty);
