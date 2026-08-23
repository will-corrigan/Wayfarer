namespace Wayfarer.Core.Ui;

/// <summary>Whether the target is on the player's level, above them, or below them. Decided by
/// <see cref="Elevation.Classify"/>, which owns both the threshold and the judgement about whether
/// the target's height can be trusted at all.</summary>
public enum ElevationHint
{
    /// <summary>Near enough level, or not knowable. The readout says nothing at all in this case,
    /// which is the common one.</summary>
    Level,

    /// <summary>Meaningfully above the player — another floor, a cliff top, an upper tier.</summary>
    Above,

    /// <summary>Meaningfully below the player.</summary>
    Below,
}
