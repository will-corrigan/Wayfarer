namespace Wayfarer.Core.Ui;

/// <summary>Whether the player is outside or inside a "search this area" quest objective's radius.
/// Decided by <see cref="SearchArea.Classify"/>, which owns both the boundary and the hysteresis
/// that keeps it from flickering as the player walks back and forth across the edge.</summary>
public enum SearchAreaHint
{
    /// <summary>Not a search-area objective right now — an ordinary point objective, or no live
    /// target at all. The readout says nothing special; this is the common case and it must not
    /// change how a point objective reads.</summary>
    NotApplicable,

    /// <summary>Outside the circle. The arrow still points at its centre — the best available
    /// heading — but the readout must say it is an area to search, not a precise target.</summary>
    Outside,

    /// <summary>Inside the circle. The centre is no longer a meaningful point to walk towards: the
    /// readout stops implying a precise target and tells the player to look around instead.</summary>
    Inside,
}
