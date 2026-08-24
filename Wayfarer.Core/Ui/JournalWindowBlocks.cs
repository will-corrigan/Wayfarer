using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>Where every part of the journal window goes, inside the gilt frame. An empty rectangle
/// means the block did not fit and must not be drawn — the same contract as
/// <see cref="DetailPaneBlocks"/> and <see cref="JournalPageBlocks"/>.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct JournalWindowBlocks(

    /// <summary>The level's black disc, and the box its number is centred in. One rectangle,
    /// because the game centres the numeral on the plate (JournalDetail <c>#9</c> over
    /// <c>#10</c>).</summary>
    ScreenRect LevelBadge,
    ScreenRect Title,
    ScreenRect Kind,

    /// <summary>The rule under the title — JournalDetail <c>#39</c>.</summary>
    ScreenRect TitleRule,
    ScreenRect StatusIcon,
    ScreenRect Status,

    /// <summary>The banner, at the 376x120 every piece of art for this slot is authored at.
    /// </summary>
    ScreenRect Banner,
    ScreenRect RewardGlyph,
    ScreenRect RewardLabel,
    ScreenRect RewardTray,
    ScreenRect RewardIcon,
    ScreenRect RewardName,
    ScreenRect DescriptionGlyph,
    ScreenRect DescriptionLabel,
    ScreenRect Description,
    ScreenRect RequirementsGlyph,
    ScreenRect RequirementsLabel,
    ScreenRect Requirements,

    /// <summary>The giver, right-aligned at the foot of the page — where the game's own journal
    /// puts the name of whoever hands the thing over.</summary>
    ScreenRect Giver,

    /// <summary>The confidence footnote, centred under everything — JournalCanvas <c>#54</c>'s
    /// register.</summary>
    ScreenRect Provenance,

    /// <summary>The rule above the button row — JournalDetail <c>#48</c>.</summary>
    ScreenRect FooterRule,

    /// <summary>The row of text buttons along the bottom edge — JournalDetail <c>#49</c>.</summary>
    ScreenRect Actions,

    /// <summary>The small square button beside them — JournalDetail <c>#53</c>, the 28x28 the
    /// player's screenshot shows as a chat icon.</summary>
    ScreenRect IconButton)
{
    /// <summary>Every block that has to stay inside the page's content box: the flowed sections and
    /// the two blocks anchored to its foot. The header band, the rules and the button row are
    /// excluded because they are the box's own edges rather than its contents.</summary>
    public IEnumerable<ScreenRect> Blocks =>
    [
        StatusIcon, Status,
        Banner,
        RewardGlyph, RewardLabel, RewardTray, RewardIcon, RewardName,
        DescriptionGlyph, DescriptionLabel, Description,
        RequirementsGlyph, RequirementsLabel, Requirements,
        Giver, Provenance,
    ];

    /// <summary>The header band's own blocks, which live above the content box.</summary>
    public IEnumerable<ScreenRect> HeaderBlocks => [LevelBadge, Title, Kind];

    /// <summary>Everything the window draws, for the containment proof.</summary>
    public IEnumerable<ScreenRect> All =>
    [
        LevelBadge, Title, Kind, TitleRule,
        StatusIcon, Status,
        Banner,
        RewardGlyph, RewardLabel, RewardTray, RewardIcon, RewardName,
        DescriptionGlyph, DescriptionLabel, Description,
        RequirementsGlyph, RequirementsLabel, Requirements,
        Giver, Provenance,
        FooterRule, Actions, IconButton,
    ];

    /// <summary>Everything except the reward tray, for the proof that nothing overlaps anything
    /// else.
    ///
    /// <para>The tray is the one block that is a <i>background</i>: it is the recessed panel the
    /// game draws its reward slots on top of (<c>Journal_Detail.tex</c> (0,28) 376x52, with the slot
    /// templates authored at x=15 inside it), so its own icon and name are meant to be over it. That
    /// relationship is asserted the other way round — as containment — rather than exempted, so the
    /// slot cannot quietly drift off its panel.</para></summary>
    public IEnumerable<ScreenRect> Foreground =>
    [
        LevelBadge, Title, Kind, TitleRule,
        StatusIcon, Status,
        Banner,
        RewardGlyph, RewardLabel, RewardIcon, RewardName,
        DescriptionGlyph, DescriptionLabel, Description,
        RequirementsGlyph, RequirementsLabel, Requirements,
        Giver, Provenance,
        FooterRule, Actions, IconButton,
    ];
}
