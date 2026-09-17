namespace Wayfarer.Core.Ui;

/// <summary>Where every part of the block under the game's banner landed, in the block's own
/// coordinates. An empty rectangle means the part is not drawn this frame.</summary>
public sealed record ScenarioTreeBlocks
{
    /// <summary>The whole block's height — the sum of its sections and the foot.</summary>
    public float Height { get; init; }

    /// <summary>The compass ring's box in the gutter beside the first line.</summary>
    public ScreenRect Arrow { get; init; }

    /// <summary>One section per line.</summary>
    public IReadOnlyList<ScreenRect> Sections { get; init; } = [];

    /// <summary>The rule above a separated line, indexed with <see cref="Sections"/>.</summary>
    public IReadOnlyList<ScreenRect> Rules { get; init; } = [];

    /// <summary>The medallion beside a marked line, indexed with <see cref="Sections"/>.</summary>
    public IReadOnlyList<ScreenRect> Markers { get; init; } = [];

    /// <summary>The words of each line, indexed with <see cref="Sections"/>.</summary>
    public IReadOnlyList<ScreenRect> Texts { get; init; } = [];

    /// <summary>Every part that draws something, for sweeping.</summary>
    public IEnumerable<ScreenRect> All =>
        new[] { Arrow }.Concat(Rules).Concat(Markers).Concat(Texts).Where(rect => !rect.IsEmpty);
}
