using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>Where every part of the detail pane goes. An empty rectangle means the block did not fit
/// and must not be drawn.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DetailPaneBlocks(
    ScreenRect Rule,
    ScreenRect Title,
    ScreenRect Kind,
    ScreenRect StatusIcon,
    ScreenRect Status,
    ScreenRect Body,
    ScreenRect RequirementsLabel,
    ScreenRect Requirements,
    ScreenRect From,
    ScreenRect Provenance,
    ScreenRect Actions,
    int BodyLines,
    int RequirementLines)
{
    /// <summary>Every block that has to stay inside the pane's content box.</summary>
    public IEnumerable<ScreenRect> Blocks =>
    [
        Title, Kind, StatusIcon, Status, Body, RequirementsLabel, Requirements, From, Provenance,
    ];
}
