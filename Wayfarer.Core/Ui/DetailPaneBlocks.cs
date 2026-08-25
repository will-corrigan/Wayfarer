using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>Where every part of the detail pane goes. An empty rectangle means the block did not fit
/// and must not be drawn.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DetailPaneBlocks(
    ScreenRect Rule,

    /// <summary>The level's black disc, and the box its number is centred in — one rectangle,
    /// because the game centres the number on the plate rather than beside it (JournalDetail
    /// <c>#9</c> is centre-aligned over <c>#10</c>).</summary>
    ScreenRect LevelBadge,
    ScreenRect Title,
    ScreenRect Kind,
    ScreenRect StatusIcon,
    ScreenRect Status,
    ScreenRect BodyGlyph,
    ScreenRect Body,
    ScreenRect RequirementsGlyph,
    ScreenRect RequirementsLabel,
    ScreenRect Requirements,
    ScreenRect RewardGlyph,
    ScreenRect RewardLabel,
    ScreenRect RewardTray,
    ScreenRect RewardIcon,
    ScreenRect RewardName,
    ScreenRect From,
    ScreenRect Provenance,
    ScreenRect Actions,
    int BodyLines,
    int RequirementLines)
{
    /// <summary>Every block that has to stay inside the pane's content box.</summary>
    public IEnumerable<ScreenRect> Blocks =>
    [
        LevelBadge, Title, Kind, StatusIcon, Status,
        BodyGlyph, Body,
        RequirementsGlyph, RequirementsLabel, Requirements,
        RewardGlyph, RewardLabel, RewardTray, RewardIcon, RewardName,
        From, Provenance,
    ];
}
