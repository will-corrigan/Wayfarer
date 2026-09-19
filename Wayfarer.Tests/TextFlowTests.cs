using Wayfarer.Presentation;

namespace Wayfarer.Tests;

/// <summary>How a line's words are placed around its keyword. Measuring is stubbed at one unit per
/// character, so a space advances by one and every width below is a character count.</summary>
public class TextFlowTests
{
    private const float Wide = 1000f;
    private const float Leading = 20f;

    [Fact]
    public void A_sentence_with_no_keyword_is_one_stretch()
    {
        var runs = TextFlow.Lay("Speak with Minfilia.", null, Measure, Wide, Leading, 2);

        var run = Assert.Single(runs);
        Assert.Equal(new LineRun("Speak with Minfilia.", false, 0f, 0f, 20f), run);
    }

    [Fact]
    public void The_keyword_is_its_own_stretch_between_the_words_around_it()
    {
        var runs = TextFlow.Lay("Use a smoke bomb on the beehive.", "Smoke Bomb", Measure, Wide, Leading, 2);

        Assert.Equal(
            [
                new LineRun("Use a", false, 0f, 0f, 5f),
                new LineRun("smoke bomb", true, 6f, 0f, 10f),
                new LineRun("on the beehive.", false, 17f, 0f, 15f),
            ],
            runs);
    }

    [Fact]
    public void The_keyword_keeps_the_sentences_own_capitalisation()
    {
        var runs = TextFlow.Lay("Use a smoke bomb on the beehive.", "Smoke Bomb", Measure, Wide, Leading, 2);

        Assert.Equal("smoke bomb", Assert.Single(runs, run => run.Keyword).Text);
    }

    [Fact]
    public void A_keyword_the_sentence_never_says_is_ignored()
    {
        var runs = TextFlow.Lay("Speak with Minfilia.", "Smoke Bomb", Measure, Wide, Leading, 2);

        Assert.DoesNotContain(runs, run => run.Keyword);
        Assert.Single(runs);
    }

    [Fact]
    public void Room_kept_for_the_keywords_icon_widens_its_stretch()
    {
        var runs = TextFlow.Lay("Use a smoke bomb.", "smoke bomb", Measure, Wide, Leading, 2, keywordLead: 4f);

        var keyword = Assert.Single(runs, run => run.Keyword);
        Assert.Equal(14f, keyword.Width);
    }

    [Fact]
    public void Words_that_do_not_fit_wrap_to_the_next_line()
    {
        var runs = TextFlow.Lay("one two three", null, Measure, 10f, Leading, 3);

        Assert.Equal(
            [
                new LineRun("one two", false, 0f, 0f, 7f),
                new LineRun("three", false, 0f, Leading, 5f),
            ],
            runs);
    }

    [Fact]
    public void Words_past_the_last_line_are_cut_short()
    {
        var runs = TextFlow.Lay("one two three", null, Measure, 10f, Leading, 1);

        var run = Assert.Single(runs);
        Assert.Equal("one two" + TextFlow.Ellipsis, run.Text);
    }

    [Fact]
    public void A_keyword_that_does_not_fit_starts_the_next_line_whole()
    {
        var runs = TextFlow.Lay("one two three", "three", Measure, 10f, Leading, 2);

        var keyword = Assert.Single(runs, run => run.Keyword);
        Assert.Equal(new LineRun("three", true, 0f, Leading, 5f), keyword);
    }

    private static float Measure(string words) => words.Length;
}
