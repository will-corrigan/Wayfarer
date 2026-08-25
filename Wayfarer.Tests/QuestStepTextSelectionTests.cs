using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

/// <summary>Pure step-text selection extracted from QuestObjectiveSource's live ToDo-sheet read.
/// Fixture text mirrors the live "Heroes of the Hour" case (quest 67782) that exposed the
/// defect: the game's own quest tracker printed "Speak with Lucia." while Wayfarer's readout,
/// reading only the (label-less) map marker, printed nothing at all.</summary>
public class QuestStepTextSelectionTests
{
    [Fact]
    public void SelectCurrentStepText_ReturnsSheetText_ForMatchingSequence()
    {
        // "Heroes of the Hour": sequence 1's sheet entry is "Speak with Lucia.", and the live
        // marker for this quest carries no label — exactly the reported gap.
        var steps = new List<QuestStepText>
        {
            new(1, "Speak with Lucia.", HasUnresolvedPlaceholder: false),
            new(2, "Enter Fortemps Manor.", HasUnresolvedPlaceholder: false),
        };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 1, markerLabel: null);

        Assert.Equal("Speak with Lucia.", result);
    }

    [Fact]
    public void SelectCurrentStepText_AdvancesWithSequence()
    {
        var steps = new List<QuestStepText>
        {
            new(1, "Speak with Lucia.", HasUnresolvedPlaceholder: false),
            new(2, "Enter Fortemps Manor.", HasUnresolvedPlaceholder: false),
        };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 2, markerLabel: null);

        Assert.Equal("Enter Fortemps Manor.", result);
    }

    [Fact]
    public void SelectCurrentStepText_PrefersSheetOverMarker_WhenBothPresent()
    {
        // The sheet is primary; a non-empty marker label must not override it even when one
        // is available (the earlier working case — "Pick up: The Ties That Bind from
        // Claribel" — must keep working, but never at the sheet's expense).
        var steps = new List<QuestStepText> { new(1, "Speak with Lucia.", HasUnresolvedPlaceholder: false) };

        var result = QuestStepTextSelection.SelectCurrentStepText(
            steps, currentSequence: 1, markerLabel: "Pick up: The Ties That Bind from Claribel");

        Assert.Equal("Speak with Lucia.", result);
    }

    [Fact]
    public void SelectCurrentStepText_FallsBackToMarkerLabel_WhenNoSheetEntryMatchesSequence()
    {
        var steps = new List<QuestStepText> { new(1, "Speak with Lucia.", HasUnresolvedPlaceholder: false) };

        var result = QuestStepTextSelection.SelectCurrentStepText(
            steps, currentSequence: 3, markerLabel: "Pick up: The Ties That Bind from Claribel");

        Assert.Equal("Pick up: The Ties That Bind from Claribel", result);
    }

    [Fact]
    public void SelectCurrentStepText_FallsBackToMarkerLabel_WhenNoStepsAtAll()
    {
        var result = QuestStepTextSelection.SelectCurrentStepText(
            steps: [], currentSequence: 1, markerLabel: "Pick up: The Ties That Bind from Claribel");

        Assert.Equal("Pick up: The Ties That Bind from Claribel", result);
    }

    [Fact]
    public void SelectCurrentStepText_ReturnsNull_WhenNeitherSourceHasAnything()
    {
        var result = QuestStepTextSelection.SelectCurrentStepText(steps: [], currentSequence: 1, markerLabel: null);

        Assert.Null(result);
    }

    [Fact]
    public void SelectCurrentStepText_ReturnsNull_WhenSequenceIsZero_EvenWithMatchingPaddingEntries()
    {
        // QuestManager.GetQuestSequence returns 0 for "not active"; TodoParams' own unused
        // slots are also seq=0 padding. A seq=0 fixture entry must never be treated as "the
        // current step" — it is the exact shape of an unused slot, not a real one.
        var steps = new List<QuestStepText> { new(0, "unused padding slot", HasUnresolvedPlaceholder: false) };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 0, markerLabel: null);

        Assert.Null(result);
    }

    [Fact]
    public void SelectCurrentStepText_SkipsEmptyText_AndFallsBackToMarker()
    {
        var steps = new List<QuestStepText> { new(1, string.Empty, HasUnresolvedPlaceholder: false) };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 1, markerLabel: "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void SelectCurrentStepText_TakesFirstConcurrentEntry_WhenSeveralShareASequence()
    {
        // "Way of the Archer" (quest 65557): sequence 2 has three concurrent kill objectives
        // ("Slay ground squirrels.", "Slay little ladybugs.", "Slay forest funguars."), all
        // live at once. The readout shows one line; the first in sheet order wins, mirroring
        // the marker-label fallback's own "first non-empty" rule one layer up.
        var steps = new List<QuestStepText>
        {
            new(2, "Slay ground squirrels.", HasUnresolvedPlaceholder: false),
            new(2, "Slay little ladybugs.", HasUnresolvedPlaceholder: false),
            new(2, "Slay forest funguars.", HasUnresolvedPlaceholder: false),
        };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 2, markerLabel: null);

        Assert.Equal("Slay ground squirrels.", result);
    }

    [Fact]
    public void SelectCurrentStepText_DoesNotRenderPlaceholderBearingText_FallsBackToMarker()
    {
        // Verified live: a "Sheet" macro payload (an item-name reference the client fills in
        // at runtime) left "Deliver a suit of steel chainmail  to Blanstyr." — the item name
        // silently dropped, a double-space gap instead of a raw token. Rendering that verbatim
        // would be worse than the marker fallback, so a flagged entry must never win.
        var steps = new List<QuestStepText>
        {
            new(1, "Deliver a suit of steel chainmail  to Blanstyr.", HasUnresolvedPlaceholder: true),
        };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 1, markerLabel: "fallback label");

        Assert.Equal("fallback label", result);
    }

    [Fact]
    public void SelectCurrentStepText_DoesNotRenderPlaceholderBearingText_FallsThroughToNull_WhenNoMarkerEither()
    {
        var steps = new List<QuestStepText> { new(1, ".", HasUnresolvedPlaceholder: true) };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 1, markerLabel: null);

        Assert.Null(result);
    }

    [Fact]
    public void SelectCurrentStepText_SkipsPlaceholderEntry_PrefersALaterCleanEntryAtSameSequence()
    {
        var steps = new List<QuestStepText>
        {
            new(1, "Deliver a suit of steel chainmail  to Blanstyr.", HasUnresolvedPlaceholder: true),
            new(1, "Speak with Blanstyr.", HasUnresolvedPlaceholder: false),
        };

        var result = QuestStepTextSelection.SelectCurrentStepText(steps, currentSequence: 1, markerLabel: null);

        Assert.Equal("Speak with Blanstyr.", result);
    }
}
