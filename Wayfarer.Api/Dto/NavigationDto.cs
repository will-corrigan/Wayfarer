namespace Wayfarer.Api.Dto;

/// <summary>Wire shape for <c>Wayfarer.Core.Navigation.NavigationState</c>, mirrored
/// field-for-field so it round-trips through the plugin's camelCase JSON serialization.
/// Kept independent of Wayfarer.Core so consumers of this contract don't need that
/// project reference.</summary>
public sealed class NavigationDto
{
    public string Mode { get; init; } = "hidden";

    public uint? QuestId { get; init; }

    public string? QuestName { get; init; }

    public string? StepLabel { get; init; }

    public string? ZoneName { get; init; }

    public float? TargetX { get; init; }

    public float? TargetY { get; init; }

    public float? TargetZ { get; init; }

    public float? DistanceYalms { get; init; }

    public uint? AetheryteId { get; init; }

    public string? AetheryteName { get; init; }

    public bool AetheryteUnlocked { get; init; }

    public string? AethernetEntryName { get; init; }

    public string? AethernetExitName { get; init; }

    public string? EntranceName { get; init; }

    public float? EntranceX { get; init; }

    public float? EntranceZ { get; init; }

    public float? RemainingYalms { get; init; }

    /// <summary>true when the arrow is guiding to something the player explicitly chose rather than
    /// the followed quest. Superseded by <see cref="SourceId"/>/<see cref="Engaged"/>; retained
    /// with its original meaning for wire compatibility.</summary>
    public bool IsPickup { get; init; }

    /// <summary>Which feature owns the arrow: "quest", "unlocks", "hunting", or null.</summary>
    public string? SourceId { get; init; }

    /// <summary>Mode indicator text, non-null whenever <see cref="Engaged"/> is true.</summary>
    public string? SourceLabel { get; init; }

    /// <summary>An explicit mode (a route, a hunt) is active rather than the followed quest.</summary>
    public bool Engaged { get; init; }

    /// <summary>"sourceId:value" — stable identity of the active objective. Changes exactly when
    /// the objective changes, and not when a live target's position is refreshed.</summary>
    public string? ObjectiveKey { get; init; }

    /// <summary>Progress within the owning feature's own plan ("2/3 kills", "68%").</summary>
    public string? ProgressText { get; init; }

    /// <summary>The target position came from a live object-table scan rather than a curated
    /// coordinate.</summary>
    public bool IsLiveTarget { get; init; }

    public int? RouteStop { get; init; }

    public int? RouteTotal { get; init; }

    public string? Reason { get; init; }

    public uint? DutyContentFinderConditionId { get; init; }
}
