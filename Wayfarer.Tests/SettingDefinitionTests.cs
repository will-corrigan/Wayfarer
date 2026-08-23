using System.Globalization;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What a setting says it is currently set to. Both presentations render this one string,
/// so a setting reads identically wherever it is shown — and a slider that shows it can no longer
/// disagree with the value behind it, which is the reported "the position sliders still read 0".</summary>
public class SettingDefinitionTests
{
    [Fact]
    public void A_scale_setting_is_read_live_rather_than_captured()
    {
        var stored = 0.5f;
        var setting = Position(() => stored * 100f, value => stored = value / 100f);

        Assert.Equal("50%", setting.CurrentValueText());

        // Something else moved the readout — a mouse drag, a preset, a resolution change. The
        // setting is a getter, not a snapshot, so it says so.
        stored = 0.62f;
        Assert.Equal("62%", setting.CurrentValueText());
    }

    [Fact]
    public void Writing_a_scale_setting_is_what_the_next_read_returns()
    {
        var stored = 0f;
        var setting = Position(() => stored * 100f, value => stored = value / 100f);

        setting.WriteValue!(75f);

        Assert.Equal(0.75f, stored, 0.001f);
        Assert.Equal("75%", setting.CurrentValueText());
    }

    [Fact]
    public void A_size_setting_keeps_the_multiplier_form()
    {
        var setting = new SettingDefinition
        {
            Id = "readout.textScale",
            Label = "Text Size",
            Kind = SettingKind.Scale,
            Minimum = 0.8f,
            Maximum = 2f,
            Step = 0.1f,
            ReadValue = () => 1.2f,
        };

        Assert.Equal(1.2f.ToString("0.0", CultureInfo.CurrentCulture) + "x", setting.CurrentValueText());
    }

    private static SettingDefinition Position(Func<float> read, Action<float> write) => new()
    {
        Id = "readout.positionX",
        Label = "Across the Screen",
        Kind = SettingKind.Scale,
        Minimum = 0f,
        Maximum = 100f,
        Step = 1f,
        ValueFormat = "0",
        ValueUnit = "%",
        ReadValue = read,
        WriteValue = write,
    };
}
