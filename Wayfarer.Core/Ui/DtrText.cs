namespace Wayfarer.Core.Ui;

/// <summary>What the server info bar entry should say this frame, which glyph (if any) belongs in
/// front of it, and whether it should carry the "something to pick up here" alert. Produced by
/// <see cref="DtrComposer"/> so the choice is exactly as testable as the readout's own text.</summary>
/// <param name="Text">The words. A few characters wide — this is the info bar, not the readout.</param>
/// <param name="Glyph">Which mode, if any, is currently engaged.</param>
/// <param name="UnlocksNearby">Draw the alert marker. This is passive information and rides
/// alongside whatever mode is engaged rather than replacing it: the whole point is that the player
/// can see there is something to grab without opening anything and without being interrupted.</param>
public sealed record DtrText(string Text, DtrGlyph Glyph, bool UnlocksNearby = false)
{
    /// <summary>The bare fallback — nothing more specific to say.</summary>
    public static DtrText Wayfarer { get; } = new("Wayfarer", DtrGlyph.None);
}
