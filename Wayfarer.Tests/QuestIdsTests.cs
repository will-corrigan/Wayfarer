using Wayfarer.Core.Quests;

namespace Wayfarer.Tests;

public class QuestIdsTests
{
    /// <summary>"Close to Home", as the quest manager numbers it and as the sheet does.</summary>
    private const ushort CloseToHome = 65;
    private const uint CloseToHomeRow = 65601;

    [Fact]
    public void ARowIdIsTheQuestIdPlusTheSheetOffset()
    {
        Assert.Equal(CloseToHomeRow, QuestIds.RowId(CloseToHome));
    }

    [Fact]
    public void ARowIdReadsBackAsTheQuestItCameFrom()
    {
        Assert.Equal(CloseToHome, QuestIds.FromRowId(QuestIds.RowId(CloseToHome)));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(65535u)]
    public void ANumberBelowTheOffsetIsNoQuest(uint rowId)
    {
        Assert.Null(QuestIds.FromRowId(rowId));
    }

    [Fact]
    public void ANumberPastTheLastQuestIsNoQuest()
    {
        Assert.Null(QuestIds.FromRowId(QuestIds.RowId(ushort.MaxValue) + 1));
    }
}
