namespace Wayfarer.Core.Navigation;

/// <summary>Immutable-by-convention snapshot of quest navigation, published once per
/// framework tick by the plugin and read by the widget and the get_navigation MCP tool.</summary>
public sealed class NavigationState
{
    public string Mode { get; init; } = Modes.Hidden;

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

    // Set when the same-zone target has been retargeted to an aethernet entry shard:
    // the arrow points at the entry (nearest the player); the exit (nearest the
    // objective) is what the user picks in the shard's travel menu.
    public string? AethernetEntryName { get; init; }

    public string? AethernetExitName { get; init; }

    // Set when a map-link marker on the CURRENT map leads toward the objective's map:
    // the widget draws an arrow to this entrance (door / zone exit).
    public string? EntranceName { get; init; }

    public float? EntranceX { get; init; }

    public float? EntranceZ { get; init; }

    /// <summary>true when the arrow is guiding to an unlock-quest pickup rather than a followed quest</summary>
    public bool IsPickup { get; init; }

    public string? Reason { get; init; }

    public static class Modes
    {
        public const string Hidden = "hidden";

        /// <summary>Logged in and unhidden, but nothing followed — widget shows only its idle face.</summary>
        public const string Idle = "idle";
        public const string SameZone = "sameZone";
        public const string OtherZone = "otherZone";
        public const string NoLocation = "noLocation";
    }
}
