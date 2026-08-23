namespace Wayfarer.Core.Ui;

/// <summary>What the server info bar entry should say this frame, and which glyph (if any)
/// belongs in front of it. Produced by <see cref="DtrComposer"/> so the choice is exactly as
/// testable as the readout's own text.</summary>
public sealed record DtrText(string Text, DtrGlyph Glyph)
{
    /// <summary>The bare fallback — nothing more specific to say.</summary>
    public static DtrText Wayfarer { get; } = new("Wayfarer", DtrGlyph.None);
}
