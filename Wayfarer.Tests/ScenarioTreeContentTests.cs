using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What Wayfarer writes under the game's own Main Scenario Guide. The game's banner already
/// names the quest, so the block under it carries the step and the route and nothing else — no
/// heading, no name, no context about other features. Everything the route lines say is the readout
/// composer's own output, unchanged; this composer only chooses which of its lines to keep.</summary>
public class ScenarioTreeContentTests
{
    [Fact]
    public void A_hidden_snapshot_draws_nothing()
    {
        var content = ScenarioTreeContent.Compose(Inputs(new NavigationState { Mode = NavigationState.Modes.Hidden }));

        Assert.True(content.IsEmpty);
        Assert.False(content.ShowArrow);
    }

    [Fact]
    public void There_is_never_a_heading_and_never_a_subject()
    {
        foreach (var inputs in new[] { HostileReadout.Inputs, HostileReadout.PlainInputs, Inputs(SameZone()) })
        {
            var content = ScenarioTreeContent.Compose(inputs);

            Assert.DoesNotContain(content.Lines, line => line.Emphasis == ReadoutEmphasis.Heading);
            Assert.DoesNotContain(content.Lines, line => line.Subject);
        }
    }

    [Fact]
    public void The_step_text_leads_when_it_says_something_the_name_does_not()
    {
        var content = ScenarioTreeContent.Compose(Inputs(SameZone() with { StepLabel = "Speak with Momodi." }));

        Assert.Equal("Speak with Momodi.", content.Lines[0].Text);
        Assert.Equal(ReadoutEmphasis.Secondary, content.Lines[0].Emphasis);
    }

    [Fact]
    public void A_step_that_merely_repeats_the_quest_name_is_dropped()
    {
        var content = ScenarioTreeContent.Compose(
            Inputs(SameZone() with { QuestName = "The Ul'dahn Envoy", StepLabel = "the ul'dahn envoy" }));

        Assert.DoesNotContain(content.Lines, line => line.Text.Contains("Envoy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_route_lines_are_the_readouts_own_with_the_context_block_cut_off()
    {
        foreach (var inputs in new[] { HostileReadout.Inputs, HostileReadout.PlainInputs })
        {
            var full = ReadoutComposer.Compose(inputs);
            var expected = full.Lines
                .Where(line => line.Emphasis != ReadoutEmphasis.Heading && !line.Subject)
                .TakeWhile(line => !line.Separated)
                .ToList();

            var content = ScenarioTreeContent.Compose(inputs);

            Assert.Equal(expected, content.Lines);
        }
    }

    [Fact]
    public void The_arrow_and_its_target_are_the_readouts_own()
    {
        var full = ReadoutComposer.Compose(HostileReadout.Inputs);

        var content = ScenarioTreeContent.Compose(HostileReadout.Inputs);

        Assert.Equal(full.ShowArrow, content.ShowArrow);
        Assert.Equal(full.TargetX, content.TargetX);
        Assert.Equal(full.TargetY, content.TargetY);
        Assert.Equal(full.TargetZ, content.TargetZ);
        Assert.Equal(full.Elevation, content.Elevation);
    }

    [Fact]
    public void Nothing_under_the_banner_is_separated_or_marked()
    {
        foreach (var inputs in new[] { HostileReadout.Inputs, HostileReadout.PlainInputs })
        {
            var content = ScenarioTreeContent.Compose(inputs);

            Assert.DoesNotContain(content.Lines, line => line.Separated);
            Assert.DoesNotContain(content.Lines, line => line.Marked);
        }
    }

    [Fact]
    public void A_line_has_a_glyph_if_and_only_if_it_has_an_action()
    {
        var content = ScenarioTreeContent.Compose(HostileReadout.Inputs);

        Assert.All(
            content.Lines,
            line => Assert.Equal(line.Action != ReadoutLineAction.None, line.Glyph != DtrGlyph.None));
    }

    private static ReadoutInputs Inputs(NavigationState state) => new() { State = state };

    private static NavigationState SameZone() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Main Scenario",
        QuestName = "The Ul'dahn Envoy",
        QuestId = 1234,
        TargetX = 12f,
        TargetY = 3f,
        TargetZ = -40f,
    };
}
