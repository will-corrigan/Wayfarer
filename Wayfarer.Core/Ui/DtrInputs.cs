namespace Wayfarer.Core.Ui;

/// <summary>What <see cref="DtrComposer"/> needs to decide what the server info bar entry says.
/// A trimmed-down cousin of <see cref="ReadoutInputs"/>: the entry is a few characters wide, so
/// unlike the readout it never shows more than one thing at a time.</summary>
public sealed record DtrInputs
{
    /// <summary>Whether an explicit mode (a hunt, an unlock route) is engaged — see
    /// <see cref="Navigation.NavigationState.Engaged"/>.</summary>
    public bool Engaged { get; init; }

    /// <summary>Chain progress, when the engaged mode is stepping through an ordered route. Takes
    /// priority over everything else, mirroring the readout heading's own "Stop N of M" rule.</summary>
    public int? RouteStop { get; init; }

    public int? RouteTotal { get; init; }

    /// <summary>True when a hunt is the active objective rather than merely running in the
    /// background — see <see cref="ReadoutInputs.HuntingIsPrimary"/>.</summary>
    public bool HuntingIsPrimary { get; init; }

    /// <summary>Precomposed "Rank N  kills/required" text, already short enough for the bar.
    /// Ignored unless <see cref="HuntingIsPrimary"/> is also true.</summary>
    public string? HuntingLabel { get; init; }

    /// <summary>How many unlocks are glanceable from here. Only shown while nothing is engaged —
    /// the same rule the readout itself uses to collapse this list to a count.</summary>
    public int NearbyUnlockCount { get; init; }
}
