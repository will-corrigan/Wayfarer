using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The heading font cannot draw everything. The reported symptom was a readout that said
/// "Hunting Log tt warrior" where the composer had written a middle dot.</summary>
public class HeadingTextTests
{
    [Fact]
    public void The_middle_dot_that_broke_the_hunting_heading_becomes_a_hyphen() =>
        Assert.Equal("Hunting Log - Warrior", HeadingText.Plain("Hunting Log · Warrior"));

    [Theory]
    [InlineData("Stop 3 — of 11", "Stop 3 - of 11")]
    [InlineData("Stop 3 – of 11", "Stop 3 - of 11")]
    [InlineData("Coeurl’s Whisker", "Coeurl's Whisker")]
    [InlineData("“The Gold Saucer”", "\"The Gold Saucer\"")]
    [InlineData("Loading…", "Loading...")]
    [InlineData("3 × Ore", "3 x Ore")]
    [InlineData("Main Scenario", "Main Scenario")]
    public void Typographic_punctuation_folds_down_to_ascii(string input, string expected) =>
        Assert.Equal(expected, HeadingText.Plain(input));

    [Fact]
    public void Plain_ascii_is_left_exactly_alone() =>
        Assert.Equal("Unlock Route (3 of 11)", HeadingText.Plain("Unlock Route (3 of 11)"));

    [Fact]
    public void Null_and_empty_are_returned_unchanged()
    {
        Assert.Equal(string.Empty, HeadingText.Plain(null));
        Assert.Equal(string.Empty, HeadingText.Plain(string.Empty));
    }

    [Fact]
    public void A_heading_that_would_fold_away_to_nothing_is_kept_as_it_was() =>

        // A wrong glyph is easier to notice and report than a mode indicator that vanished.
        Assert.Equal("テスト", HeadingText.Plain("テスト"));

    [Fact]
    public void The_result_is_always_drawable_ascii()
    {
        var folded = HeadingText.Plain("Hunting Log · warrior — 1 of 6");

        foreach (var c in folded)
        {
            Assert.InRange(c, ' ', '~');
        }
    }
}
