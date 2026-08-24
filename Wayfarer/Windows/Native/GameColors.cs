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

    /// <summary>The journal page's text, which is dark-on-cream and so cannot use any of the roles
    /// above.
    ///
    /// <para><b>Why this is a separate set at all.</b> Every other text role in this file is
    /// light-on-transparent, because everything else Wayfarer draws sits over the 3D world. The
    /// journal page is a sheet of parchment, and the page shipped wearing the readout's colours: a
    /// near-white giver line on cream, which the player photographed and could not read. Never white
    /// on parchment.</para>
    ///
    /// <para><b>What these values are, honestly.</b> Literals, not <c>UIColor</c> rows, and not read
    /// out of <c>JournalDetail</c>'s own text nodes — the game's <c>ui/uld/JournalDetail.uld</c> is
    /// not present on the machine this was written on, so its authored text colours could not be
    /// extracted, and inventing a row id would look like evidence while being a guess. What <i>is</i>
    /// measured is the contrast: each value below is stated against the cream parchment mean recorded
    /// on <see cref="BannerHeadline"/> — (200, 195, 174) — and every one of them clears the 4.5:1 a
    /// body size needs. The roles and their relative weight are the game's: near-black body text on
    /// cream, with a muted grey-brown for the section headings and the line at the foot.</para>
    ///
    /// <para><b>The one exception is not here.</b> The level badge's numeral stays light —
    /// <see cref="GameColors.Heading"/> over <see cref="GameColors.HeadingEdge"/>, exactly as
    /// JournalDetail <c>#9</c> — because the badge is the game's own black disc
    /// (<c>Journal_Detail.tex</c> (420,124)) and not parchment. There is deliberately no value for it
    /// below: it is not a parchment role.</para></summary>
    public static class JournalPage
    {
        /// <summary>The entry's name. #251D14, a very dark warm brown — 9.4:1 on the parchment. Dark
        /// enough to read as the page's heading without the bronze outline the HUD titles wear: an
        /// outline under dark letters on paper reads as a printing fault.</summary>
        public static Vector4 Title { get; } = new(0.145f, 0.114f, 0.078f, 1f);

        /// <summary>The prose. #2B2318, 8.8:1 — a shade off the title so the two read as a hierarchy
        /// rather than as one weight.</summary>
        public static Vector4 Body { get; } = new(0.169f, 0.137f, 0.094f, 1f);

        /// <summary>A section heading — Reward, Description, Requirements. #4A4234, a muted
        /// grey-brown at 5.6:1: quieter than the prose it introduces, which is the relationship the
        /// game's own page has, and still comfortably legible.</summary>
        public static Vector4 Heading { get; } = new(0.290f, 0.259f, 0.204f, 1f);

        /// <summary>The lines that are <i>about</i> the entry rather than part of it: the kind
        /// caption, the giver at the foot, the confidence footnote. #544B3D, 4.8:1 — the quietest
        /// thing on the page that is still text and not decoration.</summary>
        public static Vector4 Meta { get; } = new(0.329f, 0.294f, 0.239f, 1f);
    }
}
