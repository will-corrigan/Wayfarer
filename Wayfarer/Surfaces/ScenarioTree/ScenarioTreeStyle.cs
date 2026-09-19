namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>How the block is drawn, as the player set it: the type sizes, the space between the
/// lines, and the compass. Saved as <c>scenario-tree.json</c>. Every value has a floor and a
/// ceiling, applied by <see cref="Clamped"/>, so a hand-edited file cannot draw nonsense.</summary>
internal sealed class ScenarioTreeStyle
{
    public const uint SmallestFont = 10;
    public const uint LargestFont = 24;
    public const float SmallestLineGap = 0f;
    public const float LargestLineGap = 24f;
    public const float SmallestCompass = 18f;
    public const float LargestCompass = 48f;

    /// <summary>Type is drawn with two pixels of air above and below a font size, the game's own habit.</summary>
    private const float LeadingAboveFont = 2f;

    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Font size of the entry line, the step being guided to.</summary>
    public uint EntryFontSize { get; set; } = 16;

    /// <summary>Font size of the route line under it.</summary>
    public uint RouteFontSize { get; set; } = 14;

    /// <summary>Space between the entry's last line and the route line, in pixels.</summary>
    public float LineGap { get; set; } = 10f;

    /// <summary>The compass's side, in pixels.</summary>
    public float CompassSize { get; set; } = 26f;

    /// <summary>Where the compass sits.</summary>
    public CompassPlacement Compass { get; set; } = CompassPlacement.Right;

    /// <summary>The line height for a font size.</summary>
    public static float LeadingFor(uint fontSize) => fontSize + (2f * LeadingAboveFont);

    /// <summary>A copy with every value inside its bounds.</summary>
    public ScenarioTreeStyle Clamped() => new()
    {
        Version = Version,
        EntryFontSize = Math.Clamp(EntryFontSize, SmallestFont, LargestFont),
        RouteFontSize = Math.Clamp(RouteFontSize, SmallestFont, LargestFont),
        LineGap = Math.Clamp(LineGap, SmallestLineGap, LargestLineGap),
        CompassSize = Math.Clamp(CompassSize, SmallestCompass, LargestCompass),
        Compass = Enum.IsDefined(Compass) ? Compass : CompassPlacement.Right,
    };
}
