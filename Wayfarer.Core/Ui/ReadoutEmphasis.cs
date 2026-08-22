namespace Wayfarer.Core.Ui;

/// <summary>How much weight a readout line carries. Four levels, because the readout's whole job is
/// to make one thing obviously the thing and everything else obviously not — the complaint that
/// produced this type was that a hunt in progress and an unrelated followed quest were drawn
/// identically, so the player could not tell which one the arrow was pointing at.</summary>
public enum ReadoutEmphasis
{
    /// <summary>Names the active mode. One per readout, at the top, in the game's panel-title
    /// style. This is the mode indicator; there is no separate badge.</summary>
    Heading,

    /// <summary>The thing the arrow is following, and the distance to it.</summary>
    Primary,

    /// <summary>Detail about the primary: the objective step, routing advice, the next leg.</summary>
    Secondary,

    /// <summary>Context that is deliberately NOT competing: the ambient quest while a mode is
    /// engaged, nearby-unlock counts, a hunting line that is not the current objective.</summary>
    Muted,
}
