using System.Numerics;
using KamiToolKit.Classes;

namespace Wayfarer.App.Settings;

/// <summary>The game's own themed UI colours, read from the <c>UIColor</c> sheet so everything
/// Wayfarer draws follows the player's interface theme. The fallbacks are the Dark theme's values
/// of the same rows, used only if the lookup is unavailable.</summary>
internal static class GameColors
{
    /// <summary>General HUD body text: white.</summary>
    public static Vector4 Body => Get(1, new Vector4(1f, 1f, 1f, 1f));

    /// <summary>The game's standard body-text edge: teal-blue.</summary>
    public static Vector4 BodyEdge => Get(53, new Vector4(0.039f, 0.412f, 0.573f, 1f));

    /// <summary>Secondary, dimmed label text: mid-grey.</summary>
    public static Vector4 Dimmed => Get(3, new Vector4(0.627f, 0.627f, 0.627f, 1f));

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
