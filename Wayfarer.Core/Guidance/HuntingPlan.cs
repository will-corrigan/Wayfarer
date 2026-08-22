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
    public static bool IsComplete(int killed, int required) => killed >= required;

    public static string ProgressText(int killed, int required) => $"{killed}/{required}";

    /// <summary>"Hunting Log · Gladiator" — the mode indicator. Falls back to the bare log name
    /// when the active log has not resolved yet.</summary>
    public static string SourceLabel(string? activeLogLabel) =>
        activeLogLabel is { Length: > 0 } label ? $"Hunting Log · {label}" : "Hunting Log";

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
