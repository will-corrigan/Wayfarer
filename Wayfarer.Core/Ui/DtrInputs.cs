namespace Wayfarer.Core.Ui;

/// <summary>What <see cref="DtrComposer"/> needs to decide what the server info bar entry says.
/// A trimmed-down cousin of <see cref="ReadoutInputs"/>: the entry is a few characters wide, so
/// unlike the readout it never shows more than one thing at a time.</summary>
public sealed record DtrInputs
{
    /// <summary>Whether an explicit mode is engaged — see
    /// <see cref="Navigation.NavigationState.Engaged"/>.</summary>
    public bool Engaged { get; init; }

    /// <summary>Chain progress, when the engaged mode is stepping through an ordered route.</summary>
    public int? RouteStop { get; init; }

    public int? RouteTotal { get; init; }

    /// <summary>What the player actually has to do next. This, and not which feature owns the
    /// arrow, is what decides the entry's glyph — see <see cref="DtrGlyph"/>.</summary>
    public DtrNextStep Step { get; init; }

    /// <summary>Where the next step goes, when it has a name: the aetheryte to teleport to, or the
    /// aethernet shard to come out at. Ignored for a walk.</summary>
    public string? StepTarget { get; init; }

    /// <summary>How far away the thing being walked to is. Shown only for a walk — a distance
    /// beside a teleport is the distance to somewhere the player is not going yet.</summary>
    public float? DistanceYalms { get; init; }
}
