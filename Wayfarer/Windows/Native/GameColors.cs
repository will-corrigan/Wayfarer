using System.Numerics;
using KamiToolKit.Classes;

namespace Wayfarer.Windows.Native;

/// <summary>The game's own themed UI colours, read live from the <c>UIColor</c> sheet through
/// <c>AtkUIColorHolder</c> rather than hardcoded, so everything Wayfarer draws stays correct when
/// the player switches to the Light or Classic FF interface theme. Row ids are the ones the game's
/// own HUD panels use; the fallbacks are the Dark-theme values of those same rows, used only if the
/// lookup is unavailable (no <c>AtkStage</c> yet — e.g. very early in load).</summary>
internal static class GameColors
{
    /// <summary>Panel-title fill: white. Paired with <see cref="HeadingEdge"/>.</summary>
    public static Vector4 Heading => Get(50, new Vector4(1f, 1f, 1f, 1f));

    /// <summary>Panel-title outline: warm bronze-gold. This pairing is what makes a title read as
    /// a vanilla HUD panel header rather than as plugin text.</summary>
    public static Vector4 HeadingEdge => Get(54, new Vector4(0.557f, 0.416f, 0.047f, 1f));

    /// <summary>General HUD body text: white.</summary>
    public static Vector4 Body => Get(1, new Vector4(1f, 1f, 1f, 1f));

    /// <summary>The game's standard body-text edge/glow: teal-blue.</summary>
    public static Vector4 BodyEdge => Get(53, new Vector4(0.039f, 0.412f, 0.573f, 1f));

    /// <summary>Secondary / dimmed label text: mid-grey. Also the "done" state.</summary>
    public static Vector4 Dimmed => Get(3, new Vector4(0.627f, 0.627f, 0.627f, 1f));

    /// <summary>List text, warm cream — the Duty Finder's own row colour.</summary>
    public static Vector4 ListText => Get(8, new Vector4(0.933f, 0.882f, 0.773f, 1f));

    /// <summary>Reserved for genuinely bad states only. Never the sole signal for one.</summary>
    public static Vector4 Bad => Get(17, new Vector4(0.863f, 0f, 0f, 1f));

    /// <summary>Complete / available.</summary>
    public static Vector4 Good => Get(45, new Vector4(0f, 0.8f, 0.133f, 1f));

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
