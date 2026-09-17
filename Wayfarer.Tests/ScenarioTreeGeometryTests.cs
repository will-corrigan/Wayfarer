using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Wayfarer's sub-line geometry was measured off the game's own Main Scenario Guide, and
/// the guidance node now sits inside that very addon. These proofs tie the two sets of numbers
/// together: placed at <see cref="GameMetrics.ScenarioTree.GuidanceLeft"/>, our marker gutter has to
/// land on the game's icon column and our words on the game's text column, or the lines would read
/// as a second, misaligned block under the banner.</summary>
public class ScenarioTreeGeometryTests
{
    [Fact]
    public void Our_marker_gutter_lands_on_the_games_own_icon_column()
    {
        Assert.Equal(
            GameMetrics.ScenarioTree.RowIconLeft,
            GameMetrics.ScenarioTree.GuidanceLeft + GameMetrics.Banner.MarkerLeft);
    }

    [Fact]
    public void Our_words_land_on_the_games_own_text_column()
    {
        Assert.Equal(
            GameMetrics.ScenarioTree.RowTextLeft,
            GameMetrics.ScenarioTree.GuidanceLeft + GameMetrics.Banner.SubLineLeft);
    }

    [Theory]
    [InlineData(0, 54f)]
    [InlineData(1, 80f)]
    [InlineData(2, 106f)]
    public void The_block_starts_on_the_pitch_after_the_last_job_quest_row(int rows, float expected)
    {
        Assert.Equal(expected, GameMetrics.ScenarioTree.GuidanceTop(rows));
    }

    [Fact]
    public void A_negative_row_count_is_treated_as_none()
    {
        Assert.Equal(GameMetrics.ScenarioTree.RowsTop, GameMetrics.ScenarioTree.GuidanceTop(-3));
    }

    [Fact]
    public void The_block_at_the_games_own_size_never_leaves_the_games_own_root()
    {
        Assert.True(
            GameMetrics.ScenarioTree.GuidanceLeft + GameMetrics.Banner.Width <= GameMetrics.ScenarioTree.RootWidth);
    }
}
