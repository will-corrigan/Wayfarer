using Wayfarer.Core.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>How the block is drawn, as the player set it: the type sizes, the space between the
/// lines, and the compass. Saved as <c>scenario-tree.json</c>. Every value has a floor and a
/// ceiling, applied by <see cref="Clamped"/>, so a hand-edited file cannot draw nonsense.</summary>
internal sealed class ScenarioTreeStyle
{
    /// <summary>What each value may be set between. The compass stops at the size of the art it is
    /// drawn from, past which it only blurs.</summary>
    public const uint SmallestFont = 10;

    /// <inheritdoc cref="SmallestFont"/>
    public const uint LargestFont = 28;

    /// <inheritdoc cref="SmallestFont"/>
    public const float SmallestLineGap = 0f;

    /// <inheritdoc cref="SmallestFont"/>
    public const float LargestLineGap = 24f;

    /// <inheritdoc cref="SmallestFont"/>
    public const float SmallestInset = 0f;

    /// <inheritdoc cref="SmallestFont"/>
    public const float LargestInset = 120f;

    /// <inheritdoc cref="SmallestFont"/>
    public const float SmallestCompass = 18f;

    /// <inheritdoc cref="SmallestFont"/>
    public const float LargestCompass = GlyphCanvas.Size;

    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Font size of the entry line, the step being guided to.</summary>
    public uint EntryFontSize { get; set; } = 16;

    /// <summary>Font size of the route line under it.</summary>
    public uint RouteFontSize { get; set; } = 14;

    /// <summary>Space between the entry's last line and the route line, in pixels.</summary>
    public float LineGap { get; set; } = 3f;

    /// <summary>How far in from the guide's left edge the block starts. The guide has three edges
    /// worth lining up with: the plate's own icon at 13, the job-quest rows' icons at 44, and their
    /// words at 72.</summary>
    public float ContentLeft { get; set; } = 44f;

    /// <summary>The compass's side, in pixels.</summary>
    public float CompassSize { get; set; } = 36f;

    /// <summary>Where the compass sits.</summary>
    public CompassPlacement Compass { get; set; } = CompassPlacement.Right;

    /// <summary>A copy with every value inside its bounds.</summary>
    public ScenarioTreeStyle Clamped() => new()
    {
        Version = Version,
        EntryFontSize = Math.Clamp(EntryFontSize, SmallestFont, LargestFont),
        RouteFontSize = Math.Clamp(RouteFontSize, SmallestFont, LargestFont),
        LineGap = Math.Clamp(LineGap, SmallestLineGap, LargestLineGap),
        ContentLeft = Math.Clamp(ContentLeft, SmallestInset, LargestInset),
        CompassSize = Math.Clamp(CompassSize, SmallestCompass, LargestCompass),
        Compass = Enum.IsDefined(Compass) ? Compass : CompassPlacement.Right,
    };
}
