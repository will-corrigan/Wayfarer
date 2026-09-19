using Wayfarer.Modules.Quests;

namespace Wayfarer.Tests;

/// <summary>The two numberings a quest has, and that they convert both ways.</summary>
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
        Assert.Equal(CloseToHome, QuestIds.FromAnyId(QuestIds.RowId(CloseToHome)));
    }

    [Fact]
    public void TheQuestsOwnIdReadsBackAsItself()
    {
        Assert.Equal(CloseToHome, QuestIds.FromAnyId(CloseToHome));
    }

    [Fact]
    public void ZeroIsNoQuest()
    {
        Assert.Null(QuestIds.FromAnyId(0));
    }

    [Fact]
    public void ANumberPastTheLastQuestIsNoQuest()
    {
        Assert.Null(QuestIds.FromAnyId(QuestIds.RowId(ushort.MaxValue) + 1));
    }
}
