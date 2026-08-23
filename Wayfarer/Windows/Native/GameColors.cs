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

    /// <summary>The headline written across the readout's banner plate.
    ///
    /// <para><b>Dark, which nothing else in this file is, and the one value here not backed by
    /// evidence.</b> Every other HUD text role in the plugin is light-on-transparent because it is
    /// drawn over the 3D world. The banner's plate is a sheet of cream parchment — mean
    /// (200, 195, 174), sampled straight off <c>ui/uld/ScenarioTree.tex (0,0) 300x48</c> across the
    /// whole of its stretchable band — so a white headline on it would be invisible.
    /// The game's own headline node resolves its colour through <c>IsUIColor</c> like everything else
    /// on that layout, but WHICH <c>UIColor</c> row could not be established from the files, and
    /// guessing a row would be worse than admitting a literal: a wrong row id looks deliberate and
    /// reads as evidence.</para>
    ///
    /// <para>So this is a literal, chosen against the sampled parchment: a dark warm brown, in the
    /// same family as the bronze the plate's own bevel is drawn in, and 8.8:1 against that mean —
    /// comfortably past the 4.5:1 a body size needs, which is the one thing about it that IS
    /// measured. <b>The hue is not.</b> The check is one screenshot of the game's own Main Scenario
    /// Guide beside ours; until somebody takes it, this is a legible value rather than the right
    /// one.</para></summary>
    public static Vector4 BannerHeadline { get; } = new(0.180f, 0.129f, 0.075f, 1f);

    /// <summary>The headline's outline — less a colour than a halo. The parchment has visible noise
    /// in it, and a pale edge under dark letters is what keeps them crisp on a textured ground.
    /// Paired with <see cref="BannerHeadline"/>, and unverified for the same reason.</summary>
    public static Vector4 BannerHeadlineEdge { get; } = new(0.925f, 0.906f, 0.831f, 1f);

    /// <summary>What the game's own drop-down arrow is multiplied by to sit on the plate.
    ///
    /// <para>The art at <c>ui/uld/DropDownA.tex (44,0) 12x12</c> is a near-white triangle —
    /// extracted and looked at — which is right on the grey field the game draws it over and
    /// invisible on cream parchment. Multiplying rather than replacing keeps the glyph's own shading
    /// and anti-aliasing, and the product lands in the same brown family as
    /// <see cref="BannerHeadline"/>, so the switcher reads as part of the headline rather than as a
    /// mark left on it.</para></summary>
    public static Vector3 BannerControlTint { get; } = new(0.22f, 0.17f, 0.11f);

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
