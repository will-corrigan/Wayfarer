using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

public class DisplayNamesTests
{
    [Theory]
    [InlineData("dragonfly", "Dragonfly")]
    [InlineData("wharf rat", "Wharf Rat")]
    [InlineData("little ladybug", "Little Ladybug")]
    public void Sheet_names_are_title_cased_the_way_the_hunting_log_shows_them(string sheet, string shown) =>
        Assert.Equal(shown, DisplayNames.TitleCase(sheet));

    [Fact]
    public void An_apostrophe_does_not_start_a_new_word()
    {
        Assert.Equal("Coeurl's Whisker", DisplayNames.TitleCase("coeurl's whisker"));
        Assert.Equal("Ked's Wolf", DisplayNames.TitleCase("ked's wolf"));
    }

    [Fact]
    public void Joining_words_stay_lower_case_in_the_middle_of_a_name() =>
        Assert.Equal("Apkallu of Paradise", DisplayNames.TitleCase("apkallu of paradise"));

    [Fact]
    public void A_joining_word_that_opens_or_closes_a_name_is_still_capitalised()
    {
        Assert.Equal("The Behemoth", DisplayNames.TitleCase("the behemoth"));
        Assert.Equal("Bringer of Doom To", DisplayNames.TitleCase("bringer of doom to"));
    }

    [Fact]
    public void A_word_the_sheet_already_capitalised_is_left_exactly_as_written()
    {
        Assert.Equal("Ked", DisplayNames.TitleCase("Ked"));
        Assert.Equal("IIIrd Cohort Vanguard", DisplayNames.TitleCase("IIIrd cohort vanguard"));
    }

    [Fact]
    public void Numerals_and_punctuation_survive_unchanged() =>
        Assert.Equal("2nd Cohort Hoplomachus", DisplayNames.TitleCase("2nd cohort hoplomachus"));

    [Fact]
    public void Empty_input_is_passed_straight_through()
    {
        Assert.Equal(string.Empty, DisplayNames.TitleCase(null));
        Assert.Equal(string.Empty, DisplayNames.TitleCase(string.Empty));
        Assert.Equal("   ", DisplayNames.TitleCase("   "));
    }
}
