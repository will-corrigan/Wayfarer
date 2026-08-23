namespace Wayfarer.Core.Ui;

/// <summary>When the readout is allowed to claim the target is above or below the player.
///
/// <para><b>Why this is a decision and not a comparison.</b> The game itself hangs a small up/down
/// chevron off a minimap marker that is on a different floor, and that is the convention being
/// mirrored — but the game knows which floor a marker is on, and this does not. All this has is two
/// Y coordinates, and the ground under a player's feet moves by a yalm or two just from running
/// across a field. A naive comparison would flag every hillock, which is worse than saying nothing:
/// an indicator that is on most of the time carries no information.</para></summary>
public static class Elevation
{
    /// <summary>How far above or below the player the target has to be before the readout says so.
    ///
    /// <para>Six yalms. A yalm is about a metre, which anchors the reasoning: a jump clears roughly
    /// two, ordinary terrain roll under a running player is another two or three, and a storey of a
    /// building is six to eight. Six is therefore the smallest number that is clearly not terrain
    /// and clearly is a different level of the world — the case the player actually wants flagged,
    /// where walking straight at the arrow will not get them there.</para></summary>
    public const float ShowAtYalms = 6f;

    /// <summary>How far the difference has to fall back before the readout stops saying it.
    ///
    /// <para>A single threshold flickers: a target sitting at exactly the threshold blinks its
    /// indicator on and off as the player walks up and down a ramp, which is the most distracting
    /// possible behaviour for a hint that is meant to be glanced at. Two yalms of hysteresis — one
    /// jump's worth — is enough that ordinary movement cannot cross both bounds.</para></summary>
    public const float HideAtYalms = 4f;

    /// <summary>Classifies a vertical difference, given what the readout was saying last frame.
    ///
    /// <para><paramref name="verticalDelta"/> is the target's height minus the player's, in yalms,
    /// and is <see langword="null"/> when the target's height is not known or not trustworthy —
    /// in which case the answer is <see cref="ElevationHint.Level"/>, because the readout must not
    /// draw a confident arrow off a coordinate it does not believe.</para></summary>
    /// <param name="verticalDelta">Target Y minus player Y, in yalms, or null when unknown.</param>
    /// <param name="previous">What was shown last frame, which is what supplies the hysteresis.</param>
    public static ElevationHint Classify(float? verticalDelta, ElevationHint previous = ElevationHint.Level)
    {
        if (verticalDelta is not { } delta || float.IsNaN(delta))
        {
            return ElevationHint.Level;
        }

        var wanted = delta > 0f ? ElevationHint.Above : ElevationHint.Below;
        var magnitude = Math.Abs(delta);

        // Already showing this direction: keep showing it until it falls back past the lower bound.
        if (previous == wanted)
        {
            return magnitude >= HideAtYalms ? wanted : ElevationHint.Level;
        }

        return magnitude >= ShowAtYalms ? wanted : ElevationHint.Level;
    }

    /// <summary>How the hint reads on the distance line, or null when there is nothing to say.
    /// Sentence case and second person, like the rest of the readout's content.</summary>
    public static string? Words(ElevationHint hint) => hint switch
    {
        ElevationHint.Above => "above you",
        ElevationHint.Below => "below you",
        _ => null,
    };
}
