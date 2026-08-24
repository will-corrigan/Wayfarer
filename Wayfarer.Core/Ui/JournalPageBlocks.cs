using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>Where every part of the journal page goes. An empty rectangle means the block did not
/// fit and must not be drawn — the same contract as <see cref="DetailPaneBlocks"/>.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct JournalPageBlocks(
    ScreenRect Rule,

    /// <summary>The level's black disc, and the box its number is centred in — one rectangle,
    /// because the game centres the numeral on the plate (JournalDetail <c>#9</c> over
    /// <c>#10</c>).</summary>
    ScreenRect LevelBadge,
    ScreenRect Title,
    ScreenRect Kind,

    /// <summary>The rule under the title, JournalDetail <c>#39</c>'s place in the page.</summary>
    ScreenRect TitleRule,

    /// <summary>The banner, at the 376x120 the game authors every piece of art for this slot at.
    /// </summary>
    ScreenRect Banner,
    ScreenRect RewardGlyph,
    ScreenRect RewardLabel,
    ScreenRect RewardTray,
    ScreenRect RewardIcon,
    ScreenRect RewardName,
    ScreenRect StatusIcon,
    ScreenRect Status,
    ScreenRect RequirementsGlyph,
    ScreenRect RequirementsLabel,
    ScreenRect Requirements,
    ScreenRect DescriptionGlyph,
    ScreenRect DescriptionLabel,
    ScreenRect Description,
    ScreenRect InformationGlyph,
    ScreenRect InformationLabel,
    ScreenRect Information,

    /// <summary>The confidence footnote, centred under both columns.</summary>
    ScreenRect Provenance,

    /// <summary>The rule above the action row.</summary>
    ScreenRect FooterRule,

    /// <summary>Back, and the entry's actions, pinned to the bottom edge.</summary>
    ScreenRect Actions,
    int DescriptionLines,
    int RequirementLines,
    int InformationLines)
{
    /// <summary>Every block that has to stay inside the page's content box. The three rules and the
    /// action row are excluded because they are the box's own edges, not contents.</summary>
    public IEnumerable<ScreenRect> Blocks =>
    [
        Banner,
        RewardGlyph, RewardLabel, RewardTray, RewardIcon, RewardName,
        StatusIcon, Status,
        RequirementsGlyph, RequirementsLabel, Requirements,
        DescriptionGlyph, DescriptionLabel, Description,
        InformationGlyph, InformationLabel, Information,
        Provenance,
    ];

    /// <summary>The header's own blocks, which live above the content box in their own band.
    /// </summary>
    public IEnumerable<ScreenRect> HeaderBlocks => [LevelBadge, Title, Kind];
}
