using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>How wide a control on the Settings tab may be. These exist because the readout-position
/// sliders were reported as "clipping outside the border of the screen viewable bit": they were
/// stretched to the scrolling container's full width, and the container draws its scroll bar inside
/// that width and clips at its own edge.</summary>
public class SettingsLayoutTests
{
    [Fact]
    public void A_control_never_reaches_the_container_edge()
    {
        const float container = 640f;

        var width = SettingsLayout.ControlWidth(container);

        Assert.True(width < container, "a control that is as wide as its container runs under the scroll bar");
        Assert.Equal(container - SettingsLayout.ScrollGutter, width, 0.01f);
    }

    [Theory]
    [InlineData(460f)]
    [InlineData(560f)]
    [InlineData(760f)]
    [InlineData(1200f)]
    public void The_reserved_gutter_is_the_same_at_every_window_width(float container)
    {
        Assert.Equal(SettingsLayout.ScrollGutter, container - SettingsLayout.ControlWidth(container), 0.01f);
    }

    [Fact]
    public void A_pathologically_narrow_window_still_gets_a_usable_control()
    {
        // Better to overflow a window nobody can read anyway than to draw a slider narrower than
        // its own handle.
        Assert.Equal(SettingsLayout.MinimumControlWidth, SettingsLayout.ControlWidth(40f), 0.01f);
        Assert.Equal(SettingsLayout.MinimumControlWidth, SettingsLayout.ControlWidth(0f), 0.01f);
    }
}
