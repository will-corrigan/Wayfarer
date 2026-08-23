using System.Numerics;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The readout must not move on its own.
///
/// <para>The defect these come from was reported live and is worth writing down exactly: with an
/// unlock line on screen the readout was "constantly flying up and down the screen at rapid pace".
/// That is a feedback loop, not a rendering fault — where the readout sat was computed from how tall
/// its content had measured, so any line that came and went on alternate frames moved the whole
/// readout once per frame. The fix is that placement cannot see the measured height at all, and
/// these are the tests that keep it that way.</para></summary>
public class ReadoutStabilityTests
{
    private static readonly Vector2 Screen = new(1920f, 1080f);

    public static TheoryData<ReadoutPosition> EveryPreset()
    {
        var data = new TheoryData<ReadoutPosition>();
        foreach (var preset in Enum.GetValues<ReadoutPosition>())
        {
            data.Add(preset);
        }

        return data;
    }

    /// <summary>The load-bearing property. A readout three lines tall and one twelve lines tall are
    /// placed in exactly the same spot, so a line appearing changes where the readout <i>ends</i>
    /// and nothing else.</summary>
    [Theory]
    [MemberData(nameof(EveryPreset))]
    public void Where_the_readout_sits_does_not_depend_on_how_tall_its_content_is(ReadoutPosition preset)
    {
        var shortest = ReadoutLayout.PlacementSize(new Vector2(320f, 60f));
        var tallest = ReadoutLayout.PlacementSize(new Vector2(320f, 300f));

        Assert.Equal(ReadoutLayout.Anchor(preset, shortest, Screen), ReadoutLayout.Anchor(preset, tallest, Screen));
        Assert.Equal(
            ReadoutLayout.FromFraction(new Vector2(0.5f, 0.5f), shortest, Screen),
            ReadoutLayout.FromFraction(new Vector2(0.5f, 0.5f), tallest, Screen));
    }

    /// <summary>The exact shape of the reported loop, simulated: content that gains and loses a line
    /// on alternate frames, with the placement re-run each frame. Every frame has to agree.</summary>
    [Fact]
    public void A_line_that_appears_and_disappears_every_frame_does_not_move_the_readout()
    {
        var positions = new List<Vector2>();
        for (var frame = 0; frame < 8; frame++)
        {
            // One muted line's worth of height, on and off.
            var measured = new Vector2(320f, frame % 2 == 0 ? 148f : 166f);
            var size = ReadoutLayout.PlacementSize(measured);
            positions.Add(ReadoutLayout.Anchor(ReadoutPosition.BottomCentre, size, Screen));
        }

        Assert.Single(positions.Distinct());
    }

    [Fact]
    public void The_obstacle_dodge_cannot_be_moved_by_a_change_in_content_height()
    {
        // The minimap, where it lives on a default HUD, overlapping a top-right readout.
        var minimap = new ScreenRect(new Vector2(1550f, 20f), new Vector2(340f, 200f));

        var shortest = ReadoutLayout.PlacementSize(new Vector2(320f, 60f));
        var tallest = ReadoutLayout.PlacementSize(new Vector2(320f, 300f));

        var a = ReadoutLayout.Avoid(
            ReadoutLayout.Anchor(ReadoutPosition.TopRight, shortest, Screen), shortest, Screen, [minimap]);
        var b = ReadoutLayout.Avoid(
            ReadoutLayout.Anchor(ReadoutPosition.TopRight, tallest, Screen), tallest, Screen, [minimap]);

        Assert.Equal(a, b);
    }

    /// <summary>Placing, storing and re-placing has to be a fixed point, or the readout walks across
    /// the screen a little further every frame instead of jumping.</summary>
    [Fact]
    public void Storing_where_a_preset_landed_and_reading_it_back_returns_the_same_place()
    {
        var size = ReadoutLayout.PlacementSize(new Vector2(320f, 140f));
        var placed = ReadoutLayout.Anchor(ReadoutPosition.TopCentre, size, Screen);

        for (var round = 0; round < 5; round++)
        {
            var fraction = ReadoutLayout.ToFraction(placed, size, Screen);
            var again = ReadoutLayout.FromFraction(fraction, size, Screen);

            Assert.True(Vector2.Distance(placed, again) < 0.01f, $"round {round}: {placed} became {again}");
            placed = again;
        }
    }

    /// <summary>The reported line was an unlock with a distance in it. A distance sitting exactly on
    /// the rounding boundary must not change how many lines the readout has — only how the number
    /// reads. Rounding is for display and nothing else.</summary>
    [Theory]
    [InlineData(349.4f)]
    [InlineData(349.5f)]
    [InlineData(350.0f)]
    [InlineData(350.5f)]
    [InlineData(999.94f)]
    [InlineData(1000.0f)]
    [InlineData(1000.06f)]
    public void A_distance_on_a_rounding_boundary_never_changes_the_line_count(float distance)
    {
        var baseline = Compose(348f).Lines.Count;

        Assert.Equal(baseline, Compose(distance).Lines.Count);
    }

    /// <summary>Stable inputs, stable output — asserted over repeated composes because the composer
    /// is the one part of the readout that is pure and therefore the one part where "it changed and
    /// nobody touched it" can be ruled out completely.</summary>
    [Fact]
    public void Composing_the_same_inputs_repeatedly_produces_the_same_readout()
    {
        var first = Compose(350f);
        for (var frame = 0; frame < 5; frame++)
        {
            var again = Compose(350f);

            Assert.Equal(first.Lines.Count, again.Lines.Count);
            for (var i = 0; i < first.Lines.Count; i++)
            {
                Assert.Equal(first.Lines[i].Text, again.Lines[i].Text);
                Assert.Equal(first.Lines[i].Emphasis, again.Lines[i].Emphasis);
                Assert.Equal(first.Lines[i].Separated, again.Lines[i].Separated);
            }
        }
    }

    /// <summary>A long unlock name with a four-digit distance is exactly the line that was reported
    /// clipped. The composer's job is to hand it over whole; the body's job is to wrap it. This
    /// pins the first half — nothing truncates or ellipsises on the way through.</summary>
    [Fact]
    public void A_long_line_is_handed_to_the_readout_whole_rather_than_truncated()
    {
        const string LongName = "The Ceremony of Eternal Bonding and Everything That Comes With It";
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = SameZoneState(),
            DistanceYalms = 1234f,
            NearbyUnlocks = [$"{LongName} ({NavMath.FormatDistance(1234f)})"],
        });

        var line = content.Lines.Single(l => l.Text.Contains(LongName, StringComparison.Ordinal));

        Assert.EndsWith("(1.2k yalms)", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("...", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("…", line.Text, StringComparison.Ordinal);
    }

    private static ReadoutContent Compose(float distance) => ReadoutComposer.Compose(new ReadoutInputs
    {
        State = SameZoneState(),
        DistanceYalms = distance,
        NearbyUnlocks = [$"Chocobo Companion ({NavMath.FormatDistance(distance)})"],
    });

    private static NavigationState SameZoneState() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Main Scenario",
        QuestName = "The Company You Keep",
        TargetX = 100f,
        TargetZ = 100f,
    };
}
