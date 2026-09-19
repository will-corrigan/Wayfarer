namespace Wayfarer.Guidance;

/// <summary>When the compass may claim the target is above or below the player. The ground under
/// a running player moves by a yalm or two on its own, so this is a decision with a threshold and
/// hysteresis, not a comparison: a mark that flickers on every hillock carries no information.</summary>
public static class Elevation
{
    /// <summary>How far above or below the player the target has to be before the mark shows. A
    /// jump clears about two yalms, terrain roll another two or three, a storey six to eight: six
    /// is the smallest difference that is clearly a different level of the world.</summary>
    public const float ShowAtYalms = 6f;

    /// <summary>How far the difference has to fall back before the mark hides. Two yalms of
    /// hysteresis, one jump's worth, so ordinary movement cannot cross both bounds.</summary>
    public const float HideAtYalms = 4f;

    /// <summary>Classifies the target's height minus the player's, given what was shown last
    /// frame, which supplies the hysteresis. Null means the height is not known.</summary>
    public static ElevationHint Classify(float? riseYalms, ElevationHint previous)
    {
        if (riseYalms is not { } rise || float.IsNaN(rise))
        {
            return ElevationHint.Level;
        }

        var wanted = rise > 0f ? ElevationHint.Above : ElevationHint.Below;
        var threshold = previous == wanted ? HideAtYalms : ShowAtYalms;
        return MathF.Abs(rise) >= threshold ? wanted : ElevationHint.Level;
    }
}
