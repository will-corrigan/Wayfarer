using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The readout must not change on its own.
///
/// <para>The defect these come from was reported live and is worth writing down exactly: a line that
/// came and went on alternate frames moved the whole readout once per frame. Placement was rebuilt
/// from the ground up and no longer exists here, but the composer half of that guarantee does, and
/// these are the tests that keep it: the same inputs compose the same readout, and a number that
/// only <i>reads</i> differently never changes how many lines there are.</para></summary>
public class ReadoutStabilityTests
{
    /// <summary>A distance sitting exactly on the rounding boundary must not change how many lines
    /// the readout has — only how the number reads. Rounding is for display and nothing else.</summary>
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

    /// <summary>A long objective line is exactly the line that was reported clipped. The composer's
    /// job is to hand it over whole; the body's job is to wrap it. This pins the first half — nothing
    /// truncates or ellipsises on the way through.</summary>
    [Fact]
    public void A_long_line_is_handed_to_the_readout_whole_rather_than_truncated()
    {
        const string LongStep = "Speak with the Ceremony of Eternal Bonding attendant and everything that comes with it";
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = SameZoneState() with { StepLabel = LongStep },
            DistanceYalms = 1234f,
        });

        var line = content.Lines.Single(l => l.Text.Contains(LongStep, StringComparison.Ordinal));

        Assert.Equal(LongStep, line.Text);
        Assert.DoesNotContain("...", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("…", line.Text, StringComparison.Ordinal);
    }

    private static ReadoutContent Compose(float distance) => ReadoutComposer.Compose(new ReadoutInputs
    {
        State = SameZoneState(),
        DistanceYalms = distance,
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
