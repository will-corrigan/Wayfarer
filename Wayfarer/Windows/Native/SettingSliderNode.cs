using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>One <see cref="SettingKind.Scale"/> setting on the Settings tab: a caption that names it
/// and says what it currently reads, and the game's own slider component underneath.
///
/// <para><b>Why this type exists rather than a bare <c>FloatSliderNode</c>.</b> Two reported defects,
/// both of which come from the same place — a slider on its own says nothing at all.</para>
///
/// <list type="number">
/// <item><description><b>It had no label and no number.</b> <c>FloatSliderNode</c> draws neither: its
/// value text node is constructed <c>IsVisible = false</c>, and it has no caption of any kind. Four
/// unlabelled tracks in a column is what "the sliders do not reflect the readout's position" looks
/// like from the player's side — there was nothing on screen that could have reflected
/// anything.</description></item>
/// <item><description><b>The handle did not move to the stored value.</b>
/// <c>AtkComponentSlider</c> works out where to park its handle at the moment the value is written,
/// from the slider's width <i>at that moment</i>. Building the node, writing the value and only then
/// letting the list stretch it to the column width — which is the order an object initializer inside
/// a layout list produces — leaves the handle at the far left whatever the value is. So the value is
/// written again from <see cref="OnRecalculateLayout"/>, after any layout pass that changed the
/// width.</description></item>
/// </list>
///
/// <para><b>The value is read, never remembered.</b> <see cref="Refresh"/> pulls it back out of the
/// setting itself every time, which is what makes the readout-position sliders track a mouse drag, a
/// preset change and a resolution change without anything having to tell them.</para></summary>
internal sealed class SettingSliderNode : VerticalListNode
{
    private const float CaptionHeight = 18f;
    private const float SliderHeight = 24f;

    private readonly SettingDefinition setting;
    private readonly TextNode caption;

    private string lastCaption = string.Empty;
    private float lastApplied = float.NaN;
    private float lastWidth = float.NaN;

    /// <summary>Set while this node is writing the slider's value itself. Writing it raises the
    /// game's own <c>SliderValueUpdate</c> event exactly as a player's drag does, and the setter
    /// behind a position slider switches the readout to "Where You Put It" — so a re-seat after a
    /// layout pass would silently throw away the player's chosen preset.</summary>
    private bool applying;

    public SettingSliderNode(SettingDefinition setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        this.setting = setting;

        FitWidth = true;
        FitContents = true;
        ItemSpacing = 2f;

        caption = new TextNode
        {
            Height = CaptionHeight,
            FontType = FontType.Axis,
            FontSize = 12,
            AlignmentType = AlignmentType.TopLeft,
            TextColor = GameColors.ListText,
            TextOutlineColor = GameColors.BodyEdge,
            TextFlags = TextFlags.Edge,
        };

        Slider = new FloatSliderNode
        {
            Height = SliderHeight,
            Min = setting.Minimum,
            Max = setting.Maximum,
            Step = setting.Step,
            OnValueChanged = OnSliderMoved,
        };

        AddNode(caption);
        AddNode(Slider);

        Refresh();
    }

    /// <summary>The component the controller cursor actually lands on — the caption is a text node
    /// and cannot be focused, so scroll-follows-focus has to be wired to this.</summary>
    public FloatSliderNode Slider { get; }

    /// <summary>Re-reads the setting and puts the handle and the caption where it says. Idempotent
    /// and cheap enough for a per-tick call: it writes nothing when nothing has changed.</summary>
    public void Refresh()
    {
        var value = Math.Clamp(setting.ReadValue?.Invoke() ?? setting.Minimum, setting.Minimum, setting.Maximum);
        if (!value.Equals(lastApplied) || !Slider.Width.Equals(lastWidth))
        {
            Write(value);
        }

        UpdateCaption();
    }

    /// <inheritdoc/>
    protected override void OnRecalculateLayout()
    {
        base.OnRecalculateLayout();

        // The slider may have just been given a different width, and the handle's position was
        // computed against the old one. Re-seating is the only way to move it, and the width is what
        // decides whether it is needed — this runs on every layout pass of the whole tab.
        if (!float.IsNaN(lastApplied) && !Slider.Width.Equals(lastWidth))
        {
            Write(lastApplied);
        }
    }

    private void Write(float value)
    {
        lastApplied = value;
        lastWidth = Slider.Width;
        applying = true;
        try
        {
            Slider.Value = value;
        }
        finally
        {
            applying = false;
        }
    }

    private void OnSliderMoved(float value)
    {
        if (applying)
        {
            return;
        }

        lastApplied = value;
        lastWidth = Slider.Width;
        setting.WriteValue?.Invoke(value);
        UpdateCaption();
    }

    // Assigning String builds a SeString, and this is reachable from a per-tick refresh.
    private void UpdateCaption()
    {
        var text = $"{setting.Label}: {setting.CurrentValueText()}";
        if (string.Equals(text, lastCaption, StringComparison.Ordinal))
        {
            return;
        }

        lastCaption = text;
        caption.String = text;
    }
}
