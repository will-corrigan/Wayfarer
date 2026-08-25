namespace Wayfarer.Core.Ui;

/// <summary>Where every part of the readout landed, in the readout's own coordinates. What
/// <see cref="ReadoutBodyLayout.Compose"/> returns, and what the geometry proofs are asserted
/// against.
///
/// <para>An empty rectangle means the part is not drawn this frame, which is why every proof skips
/// empties rather than special-casing them: a hidden node has no geometry to collide with.</para>
/// </summary>
public sealed record ReadoutBodyBlocks
{
    /// <summary>The whole readout's height — the sum of its sections, and nothing else. This is what a
    /// <c>FitContents</c> container arrives at, and the host sizes itself from it.</summary>
    public float Height { get; init; }

    /// <summary>The direction-in-words fallback line, when the arrow could not be generated.</summary>
    public ScreenRect Words { get; init; }

    /// <summary>The banner section: the pill's rise and the plate, as one block. Everything on the
    /// banner lives inside this, which is what makes the banner's height independent of its content.
    /// </summary>
    public ScreenRect Banner { get; init; }

    /// <summary>The parchment plate.</summary>
    public ScreenRect Plate { get; init; }

    /// <summary>The dark pill above the plate.</summary>
    public ScreenRect Strip { get; init; }

    /// <summary>The emblem pinned to the plate's left end. It rises above the plate on purpose, which
    /// is the game's own construction and the reason the stack does not clip.</summary>
    public ScreenRect Crest { get; init; }

    /// <summary>The name of whatever is being followed, written across the plate.</summary>
    public ScreenRect Headline { get; init; }

    /// <summary>The settings cog, at the pill's right-hand end.</summary>
    public ScreenRect Cog { get; init; }

    /// <summary>The switcher's click target, over the chevron the plate's own art carries.</summary>
    public ScreenRect Switcher { get; init; }

    /// <summary>The direction indicator, in the gutter beside the line it points for: the compass
    /// ring's box, which is the outer edge of everything drawn there — the needle is concentric
    /// inside it and reaches nothing the ring does not.</summary>
    public ScreenRect Arrow { get; init; }

    /// <summary>One section per subordinate line — the box that line's rule, medallion and words all
    /// belong to. These are the flow's own output and the thing that provably cannot intersect.
    /// </summary>
    public IReadOnlyList<ScreenRect> Sections { get; init; } = [];

    /// <summary>The rule above a separated line, indexed with <see cref="Sections"/>.</summary>
    public IReadOnlyList<ScreenRect> Rules { get; init; } = [];

    /// <summary>The medallion hanging in the gutter beside a marked line, indexed with
    /// <see cref="Sections"/>. It overhangs its row by design — 32 tall against a 26-tall pitch —
    /// which is what makes it read as pinned to the line rather than as part of a column.</summary>
    public IReadOnlyList<ScreenRect> Markers { get; init; } = [];

    /// <summary>The words of each subordinate line, indexed with <see cref="Sections"/>.</summary>
    public IReadOnlyList<ScreenRect> Texts { get; init; } = [];

    /// <summary>Every part of the readout that draws something, for sweeping.</summary>
    public IEnumerable<ScreenRect> All =>
        new[] { Words, Plate, Strip, Crest, Headline, Cog, Switcher, Arrow }
            .Concat(Rules)
            .Concat(Markers)
            .Concat(Texts)
            .Where(rect => !rect.IsEmpty);
}
