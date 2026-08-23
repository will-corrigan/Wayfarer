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
    /// keeps a control's right edge off it. The bar's own thickness is fixed at 8 in the toolkit;
    /// the rest is the same 4-pixel rhythm the tab's item spacing uses.</summary>
    public const float ScrollGutter = 12f;

    /// <summary>No control is ever narrowed below this. A slider thinner than its own handle plus
    /// its value text is not a control, it is a smear — better to overflow a pathologically narrow
    /// window than to draw one.</summary>
    public const float MinimumControlWidth = 160f;

    /// <summary>The usable width for a control inside a settings container
    /// <paramref name="containerWidth"/> wide.</summary>
    public static float ControlWidth(float containerWidth) =>
        Math.Max(containerWidth - ScrollGutter, MinimumControlWidth);
}
