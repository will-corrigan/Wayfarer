using System.Numerics;
using KamiToolKit.Classes;

namespace Wayfarer.Ui;

/// <summary>The game's own themed UI colours, read from the <c>UIColor</c> sheet so everything
/// Wayfarer draws follows the player's interface theme. The fallbacks are the Dark theme's values
/// of the same rows, used only if the lookup is unavailable.</summary>
internal static class GameColors
{
    /// <summary>The <c>UIColor</c> rows the game itself uses for these parts of its interface.</summary>
    private const uint BodyRow = 1;

    /// <inheritdoc cref="BodyRow"/>
    private const uint BodyEdgeRow = 53;

    /// <inheritdoc cref="BodyRow"/>
    private const uint ListTextRow = 8;

    /// <inheritdoc cref="BodyRow"/>
    private const uint ListTextEdgeRow = 7;

    /// <inheritdoc cref="BodyRow"/>
    private const uint LinkRow = 500;

    /// <inheritdoc cref="BodyRow"/>
    private const uint LinkEdgeRow = 501;

    /// <summary>General HUD body text: white.</summary>
    public static Vector4 Body => Get(BodyRow, new Vector4(1f, 1f, 1f, 1f));

    /// <summary>The game's standard body-text edge: teal-blue.</summary>
    public static Vector4 BodyEdge => Get(BodyEdgeRow, new Vector4(0.039f, 0.412f, 0.573f, 1f));

    /// <summary>List text, warm cream: the Duty Finder's own row colour.</summary>
    public static Vector4 ListText => Get(ListTextRow, new Vector4(0.933f, 0.882f, 0.773f, 1f));

    /// <summary>The dark edge under list text, which is what keeps small text crisp rather than
    /// haloed.</summary>
    public static Vector4 ListTextEdge => Get(ListTextEdgeRow, new Vector4(0.157f, 0.157f, 0.157f, 1f));

    /// <summary>The pale blue the game prints a link in, the colour a map or item link takes in
    /// the chat log. Text in this colour reads as something to press.</summary>
    public static Vector4 Link => Get(LinkRow, new Vector4(0.627f, 0.847f, 1f, 1f));

    /// <summary>The glow the game puts behind a link, which is what makes the pale blue read as
    /// lit rather than washed out.</summary>
    public static Vector4 LinkEdge => Get(LinkEdgeRow, new Vector4(0.039f, 0.192f, 0.310f, 1f));

    private static Vector4 Get(uint rowId, Vector4 fallback)
    {
        try
        {
            return ColorHelper.GetColor(rowId);
        }
        catch (Exception)
        {
            // A colour lookup must never be the thing that stops a window from being built.
            return fallback;
        }
    }
}
