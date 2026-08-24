namespace Wayfarer.Core.Guidance;

/// <summary>Picks the label for the quest's CURRENT step — the same words the game's own quest
/// tracker prints — from the quest's own ToDo text sheet, falling back to the map marker's label
/// only when the sheet has nothing usable. Pure over the read, so the one join that is easy to
/// get backwards (which sheet row is "current" right now) is pinned by a test instead of pattern-
/// matched by inspection.
///
/// <para>THE SHEET IS PRIMARY, THE MARKER IS THE FALLBACK — deliberately the opposite of how this
/// source used to work. <see cref="QuestStepText.Sequence"/> is exactly what
/// <c>QuestManager.GetQuestSequence</c> returns, so the join needs no fuzzing: find the entry (or
/// entries — concurrent objectives at one step share a sequence, see below) whose sequence
/// matches the quest's current one. The marker's label is blank far more often than the sheet has
/// no text for the current step (that gap is the exact defect this type exists to close — see
/// "Heroes of the Hour", sequence 1, sheet text "Speak with Lucia.", live marker label
/// empty).</para>
///
/// <para>When a step has more than one concurrent objective (three simultaneous kill
/// requirements sharing one sequence is common in starting-class quests), the FIRST one in sheet
/// order wins — the readout shows one line, and "first" mirrors the marker-label fallback's own
/// "first non-empty" rule one layer up.</para></summary>
public static class QuestStepTextSelection
{
    public static string? SelectCurrentStepText(
        IReadOnlyList<QuestStepText> steps, byte currentSequence, string? markerLabel)
    {
        if (currentSequence != 0)
        {
            foreach (var step in steps)
            {
                if (step.Sequence != currentSequence)
                {
                    continue;
                }

                if (step.Text.Length == 0 || step.HasUnresolvedPlaceholder)
                {
                    continue;
                }

                return step.Text;
            }
        }

        return markerLabel is { Length: > 0 } ? markerLabel : null;
    }
}
