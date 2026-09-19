namespace Wayfarer.Ui;

/// <summary>Whether the target is on the player's level, above them, or below them. Decided by
/// <see cref="Elevation.Classify"/>, which owns the threshold.</summary>
public enum ElevationHint
{
    /// <summary>Near enough level, or not knowable. Nothing is drawn, which is the common case.</summary>
    Level,

    /// <summary>Meaningfully above the player: another floor, a cliff top, an upper tier.</summary>
    Above,

    /// <summary>Meaningfully below the player.</summary>
    Below,
}
