namespace Wayfarer.Core.Guidance;

/// <summary>The hunting source's own semantics, kept pure and testable: when a target counts as
/// done, what its destination is, and the words it puts on the readout.
///
/// Completion is a KILL COUNT and nothing else. That single fact is what the flicker defect got
/// wrong — a hunting target was pushed through a quest-pickup shape carrying quest row id 0, and
/// the navigator asked the quest system whether row 0 was accepted. Nothing outside this feature
/// can answer "is this monster done?", so nothing outside it is asked.</summary>
public static class HuntingPlan
{
    /// <summary>What this module calls itself on the readout's banner, which prints "Current" in
    /// front of it — "Current Hunting Log". Deliberately without the job qualifier that
    /// <see cref="SourceLabel"/> carries: the banner's pill names the KIND of thing being tracked,
    /// and which log it came from is a fact about the objective, not about the module. See
    /// <see cref="ObjectiveCopy.SourceName"/>.</summary>
    public const string SourceName = "Hunting Log";

    public static bool IsComplete(int killed, int required) => killed >= required;

    public static string ProgressText(int killed, int required) => $"{killed}/{required}";

    /// <summary>What the control that starts a hunt says, given how much of the RANK is left.
    ///
    /// <para><b>The rank, and not the zone.</b> Starting a hunt plans every remaining target on the
    /// current log page, grouped by zone — see <c>HuntingSource.BuildLegs</c> — so this is the number
    /// of stops the press will actually attempt. It used to be counted from the targets in the
    /// player's current zone, which is a different and much smaller set: the window's Hunting tab
    /// showed thirteen monsters over a button that said "Start Hunting (3)", and both numbers were
    /// internally correct. The label and the count are computed here, once, so the two surfaces that
    /// offer this press cannot come to disagree again.</para>
    ///
    /// <para><b>Duty-gated targets are included.</b> They have no overworld coordinate, but they are
    /// not dropped from the plan either — <see cref="Destination"/> turns them into an instanced-duty
    /// objective with the Duty Finder affordance behind it, so the press does attempt them and the
    /// count would be a lie without them.</para></summary>
    public static string StartLabel(int remainingOnRank) =>
        remainingOnRank > 0 ? $"Start Hunting ({remainingOnRank})" : "Start Hunting";

    /// <summary>Whether starting a hunt would do anything. Read from the same number
    /// <see cref="StartLabel"/> prints, so a lit button and a non-zero count are one decision — the
    /// button used to be disabled whenever the player stood in a zone with nothing left in it, while
    /// the list beside it still showed the rest of the rank waiting.</summary>
    public static bool CanStart(int remainingOnRank) => remainingOnRank > 0;

    /// <summary>"Hunting Log - Gladiator" — the mode indicator. Falls back to the bare log name
    /// when the active log has not resolved yet.
    ///
    /// <para>Two things here are fixes for what the heading actually looked like on screen:
    /// <c>"Hunting Log tt warrior"</c>. The separator was a middle dot, which the heading font
    /// (Trump Gothic) does not carry — see <see cref="Ui.HeadingText"/> — and the log name is the raw
    /// <c>ClassJob</c> sheet text, which the game stores in lower case and title-cases itself at draw
    /// time. Casing here as well as at the read boundary is deliberate: it is idempotent, and it puts
    /// the whole heading under test rather than under a live sheet read.</para></summary>
    public static string SourceLabel(string? activeLogLabel) =>
        activeLogLabel is { Length: > 0 } label
            ? Ui.HeadingText.Plain($"Hunting Log - {Ui.DisplayNames.TitleCase(label)}")
            : "Hunting Log";

    /// <summary>Where a target is. Duty-gated targets — the 25 Grand-Company-Elite ones that live
    /// inside instanced content and have no overworld coordinate — become
    /// <see cref="ObjectiveDestination.InstancedDuty"/> rather than being dropped from the plan
    /// entirely, which is what happened when the only expressible destination was a coordinate.</summary>
    public static ObjectiveDestination Destination(
        bool routable, uint territory, uint mapId, float x, float y, float z, uint? dutyTerritory, bool isLive)
    {
        if (routable)
        {
            return new ObjectiveDestination.WorldPoint(territory, mapId, x, y, z, isLive);
        }

        return dutyTerritory is { } duty
            ? new ObjectiveDestination.InstancedDuty(duty)
            : new ObjectiveDestination.Unresolved("this target only appears inside instanced content");
    }
}
