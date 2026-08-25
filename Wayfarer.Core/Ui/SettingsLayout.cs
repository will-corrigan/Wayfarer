namespace Wayfarer.Core.Ui;

/// <summary>How wide a control on the Settings tab is allowed to be.
///
/// <para><b>Why this is arithmetic and not a constant in the window.</b> The Settings tab is a
/// scrolling container, and the container's scroll bar is drawn <i>inside</i> its own width, against
/// its right edge. A control stretched to the container's full width therefore runs underneath the
/// scroll bar and past the edge the container clips at — which is the reported "the sliders clip
/// outside the border". Reserving the bar's width plus a hair of breathing room is the whole fix,
/// and it is here rather than inline so it can be tested without a game attached.</para></summary>
public static class SettingsLayout
{
    /// <summary>The width KamiToolKit's <c>ScrollingNode</c> gives its scroll bar, plus the gap that
    /// keeps a control's right edge off it. Eight is what the game's own scroll bars are — see
    /// <see cref="GameMetrics.Scroll.BarWidth"/> — and the toolkit agrees; the rest is the same
    /// four-pixel rhythm the game leaves below a rule.</summary>
    public const float ScrollGutter = GameMetrics.Scroll.BarWidth + GameMetrics.Window.RuleGap;

    /// <summary>No control is ever narrowed below this. A slider thinner than its own handle plus
    /// its value text is not a control, it is a smear — better to overflow a pathologically narrow
    /// window than to draw one.</summary>
    public const float MinimumControlWidth = 160f;

    /// <summary>A section heading inside the settings stack — the game's own section-header row,
    /// which is what every heading in this plugin's windows is set in.</summary>
    public const float HeadingHeight = GameMetrics.Row.SectionHeight;

    /// <summary>The gap the settings container leaves between two stacked items. The same
    /// below-a-rule rhythm the rest of the window uses.</summary>
    public const float ItemSpacing = GameMetrics.Window.RuleGap;

    /// <summary>A <see cref="SettingKind.Scale"/> row's caption — the line that names the setting and
    /// says what it currently reads, above the slider itself.</summary>
    public const float ScaleCaptionHeight = GameMetrics.Row.SecondaryTextHeight;

    /// <summary>The gap between a <see cref="SettingKind.Scale"/> row's caption and its slider.
    /// </summary>
    public const float ScaleCaptionGap = GameMetrics.Row.Padding;

    /// <summary>The slider on a <see cref="SettingKind.Scale"/> row.</summary>
    public const float ScaleSliderHeight = GameMetrics.Control.DropDownHeight;

    /// <summary>The usable width for a control inside a settings container
    /// <paramref name="containerWidth"/> wide.</summary>
    public static float ControlWidth(float containerWidth) =>
        Math.Max(containerWidth - ScrollGutter, MinimumControlWidth);

    /// <summary>How tall the control for a setting of this kind is.
    ///
    /// <para><b>Why the heights live here and not only in the window that builds the nodes.</b>
    /// Scroll-follows-focus is arithmetic over the stack the controls form, and the only way to prove
    /// that arithmetic without a game attached is to model the stack — which is worth nothing if the
    /// model's numbers can drift from the ones the nodes are actually built at. One declaration, read
    /// by the builder and by the proof.</para></summary>
    public static float ControlHeight(SettingKind kind) => kind switch
    {
        SettingKind.Choice => GameMetrics.Control.ButtonHeight,
        SettingKind.Scale => ScaleCaptionHeight + ScaleCaptionGap + ScaleSliderHeight,
        _ => GameMetrics.Control.CheckboxHeight,
    };
}
