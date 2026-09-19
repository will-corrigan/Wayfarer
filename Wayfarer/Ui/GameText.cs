namespace Wayfarer.Ui;

/// <summary>How the game sets type, in the one place everything Wayfarer draws asks.</summary>
internal static class GameText
{
    /// <summary>The game leaves two pixels of air above and below a line of text, whatever size it
    /// is set at.</summary>
    private const float AirAroundText = 2f;

    /// <summary>The height of one line of text at a font size: the type itself, and the game's own
    /// air above and below it.</summary>
    public static float LeadingFor(uint fontSize) => fontSize + (2f * AirAroundText);
}
