using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

public class QuestJournalSelectionTests
{
    [Fact]
    public void RecoversTheRawIdFromTheSheetRowId()
    {
        // 67782 ("Heroes of the Hour") is the exact quest verified live elsewhere in this codebase
        // (see QuestObjectiveSource's ReadQuestStepTexts doc comment) — its raw id is 67782 - 65536.
        Assert.Equal(2246u, QuestJournalSelection.RawQuestId(67782));
    }

    [Fact]
    public void IsExactAcrossTheWholeOrdinaryQuestRange_NotJustOneSample()
    {
        for (uint raw = 0; raw <= ushort.MaxValue; raw += 4001)
        {
            Assert.Equal(raw, QuestJournalSelection.RawQuestId(raw + 65536));
        }
    }

    [Fact]
    public void ZeroStaysZero()
    {
        Assert.Equal(0u, QuestJournalSelection.RawQuestId(0));
    }
}
