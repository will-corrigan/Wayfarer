using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The rule that stops an entry's lock reason from reaching the screen twice.
///
/// <para>The field report was a screenshot of one entry with the same four-line sentence printed
/// under itself: <c>UnlockStatusDisplay.Sentence</c> folds the reason into the state line, and the
/// requirements block falls back to the same reason when it has no itemised list. On the entry whose
/// gate names thirty jobs that is unmissable.</para></summary>
public class JournalRequirementTextTests
{
    [Fact]
    public void The_state_line_drops_the_reason_when_a_requirements_block_is_drawn()
    {
        var line = JournalRequirementText.StatusLine(
            "Locked",
            "Locked — needs gladiator or pugilist or marauder or lancer or archer or conjurer.",
            requirementsShown: true);

        Assert.Equal("Locked.", line);
    }

    [Fact]
    public void The_state_line_keeps_the_whole_sentence_when_there_is_no_requirements_block()
    {
        const string sentence = "Missed. No longer obtainable.";
        Assert.Equal(
            sentence,
            JournalRequirementText.StatusLine("Missed", sentence, requirementsShown: false));
    }

    [Fact]
    public void A_state_with_no_word_falls_back_to_the_sentence_rather_than_to_nothing()
    {
        const string sentence = "Available.";
        Assert.Equal(
            sentence,
            JournalRequirementText.StatusLine(string.Empty, sentence, requirementsShown: true));
    }

    [Fact]
    public void A_word_that_already_ends_in_a_stop_is_not_given_a_second_one()
    {
        Assert.Equal("Locked.", JournalRequirementText.StatusLine("Locked.", "whatever", true));
    }

    [Theory]
    [InlineData("Requirements", "Requirements")]
    [InlineData("Bedingungen", "Bedingungen")]
    [InlineData(null, "Requirements")]
    [InlineData("   ", "Requirements")]
    public void The_requirements_heading_prefers_the_games_own_word(string? gameWord, string expected)
    {
        Assert.Equal(expected, JournalRequirementText.RequirementsHeading(gameWord));
    }

    /// <summary>Addon row 479 is "This quest is not yet available." — the string
    /// <c>AddonJournalDetail</c>'s own requirements label is authored with. It is only ever offered
    /// when the thing in the way really is a quest: over a duty's or a mount's requirements it would
    /// be the game's words applied to something they are not about.</summary>
    [Fact]
    public void The_games_not_available_sentence_is_only_offered_for_a_quest_gate()
    {
        const string sentence = "This quest is not yet available.";

        Assert.Equal(sentence, JournalRequirementText.NotMetLead(sentence, gatedByQuest: true));
        Assert.Null(JournalRequirementText.NotMetLead(sentence, gatedByQuest: false));
        Assert.Null(JournalRequirementText.NotMetLead(null, gatedByQuest: true));
    }

    /// <summary>The lead sentence is not bulleted, because it is not one of the things you need — it
    /// is the sentence over them. And it costs a line out of the same budget, so a block with room for
    /// three lines shows the sentence and two bullets rather than the sentence and three.</summary>
    [Fact]
    public void The_not_met_sentence_leads_the_bullets_without_becoming_one()
    {
        var text = DetailText.Led(
            "This quest is not yet available.",
            ["Level 58 (you are 43)", "Clear Into the Aery"],
            budget: 3,
            out var drawn);

        Assert.Equal(3, drawn);
        Assert.Equal(
            "This quest is not yet available.\n• Level 58 (you are 43)\n• Clear Into the Aery",
            text);
    }

    [Fact]
    public void A_one_line_budget_shows_the_sentence_and_nothing_else()
    {
        var text = DetailText.Led("This quest is not yet available.", ["Level 58"], budget: 1, out var drawn);

        Assert.Equal(1, drawn);
        Assert.Equal("This quest is not yet available.", text);
    }

    /// <summary>The reason the heading is a reference rather than a literal — the same argument
    /// <see cref="GameTextRef"/> makes for requirement prose, and the reason
    /// <c>JournalWords</c> reads <c>Addon</c> at runtime instead of shipping English.</summary>
    [Fact]
    public void A_game_text_reference_names_a_sheet_row_and_column_rather_than_a_string()
    {
        var reference = new GameTextRef("Addon", 2835, 0);

        Assert.Equal("Addon", reference.Sheet);
        Assert.Equal(2835u, reference.Row);
        Assert.Equal(0, reference.Column);
    }
}
