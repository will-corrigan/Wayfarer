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

    public bool IsPickup { get; init; }

    public int? RouteStop { get; init; }

    public int? RouteTotal { get; init; }

    public string? Reason { get; init; }

    public uint? DutyContentFinderConditionId { get; init; }
}
