using Wayfarer.Core.Navigation;

namespace Wayfarer.Core.Ui;

/// <summary>Everything <see cref="ReadoutComposer"/> needs, gathered by the caller so the composer
/// itself stays pure and knows nothing about any particular feature.</summary>
public sealed record ReadoutInputs
{
    /// <summary>The single published guidance snapshot. Whatever this says is active is what the
    /// arrow follows — the readout never second-guesses it and never draws a second candidate.</summary>
    public required NavigationState State { get; init; }

    /// <summary>Distance to the target in yalms, measured against the player's live position this
    /// frame. Null when there is no target or no player.</summary>
    public float? DistanceYalms { get; init; }

    /// <summary>Whether the target is meaningfully above or below the player, already decided by
    /// <see cref="Ui.Elevation.Classify"/> — including the judgement about whether the target's
    /// height is trustworthy enough to say anything at all. The composer only writes it down.</summary>
    public ElevationHint Elevation { get; init; }

    /// <summary>Whether the player is outside or inside a "search this area" objective's circle,
    /// already decided (with hysteresis) by <see cref="Ui.SearchArea.Classify"/>.
    /// <see cref="SearchAreaHint.NotApplicable"/> for an ordinary point objective — the composer
    /// only writes down what it is told, exactly as with <see cref="Elevation"/>.</summary>
    public SearchAreaHint AreaHint { get; init; }
}
