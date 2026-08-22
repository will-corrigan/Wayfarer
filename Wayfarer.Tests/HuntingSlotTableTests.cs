using Wayfarer.Core.Hunting;

namespace Wayfarer.Tests;

public class HuntingSlotTableTests
{
    [Theory]
    [InlineData(1u, 0)] // GLA
    [InlineData(2u, 1)] // PGL
    [InlineData(3u, 2)] // MRD
    [InlineData(4u, 3)] // LNC
    [InlineData(5u, 4)] // ARC
    [InlineData(6u, 5)] // CNJ
    [InlineData(7u, 6)] // THM
    [InlineData(26u, 7)] // ACN
    [InlineData(29u, 11)] // ROG
    public void SlotForClassJob_BaseClasses(uint classJobId, int expectedSlot)
    {
        Assert.Equal(expectedSlot, HuntingSlotTable.SlotForClassJob(classJobId));
    }

    [Theory]
    [InlineData(19u, 0)] // PLD <- GLA
    [InlineData(20u, 1)] // MNK <- PGL
    [InlineData(21u, 2)] // WAR <- MRD
    [InlineData(22u, 3)] // DRG <- LNC
    [InlineData(23u, 4)] // BRD <- ARC
    [InlineData(24u, 5)] // WHM <- CNJ
    [InlineData(25u, 6)] // BLM <- THM
    [InlineData(27u, 7)] // SMN <- ACN
    [InlineData(28u, 7)] // SCH <- ACN
    [InlineData(30u, 11)] // NIN <- ROG
    public void SlotForClassJob_EvolvedJobsInheritBaseClassSlot(uint evolvedClassJobId, int expectedSlot)
    {
        Assert.Equal(expectedSlot, HuntingSlotTable.SlotForClassJob(evolvedClassJobId));
    }

    [Theory]
    [InlineData(31u)] // MCH
    [InlineData(32u)] // DRK
    [InlineData(33u)] // AST
    [InlineData(34u)] // SAM
    [InlineData(35u)] // RDM
    [InlineData(36u)] // BLU
    [InlineData(37u)] // GNB
    [InlineData(38u)] // DNC
    [InlineData(39u)] // RPR
    [InlineData(40u)] // SGE
    [InlineData(41u)] // VPR
    [InlineData(42u)] // PCT
    [InlineData(43u)] // BST
    public void SlotForClassJob_PostStormbloodJobs_ReturnNull(uint classJobId)
    {
        Assert.Null(HuntingSlotTable.SlotForClassJob(classJobId));
    }

    [Fact]
    public void SlotForClassJob_UnknownJob_ReturnsNull()
    {
        Assert.Null(HuntingSlotTable.SlotForClassJob(9999u));
    }

    [Theory]
    [InlineData(1u, HuntingSlotTable.EliteSlotMaelstrom)]
    [InlineData(2u, HuntingSlotTable.EliteSlotTwinAdder)]
    [InlineData(3u, HuntingSlotTable.EliteSlotImmortalFlames)]
    public void EliteSlotForGrandCompany_Cases(uint gcId, int expectedSlot)
    {
        Assert.Equal(expectedSlot, HuntingSlotTable.EliteSlotForGrandCompany(gcId));
    }

    [Fact]
    public void EliteSlotForGrandCompany_InvalidId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HuntingSlotTable.EliteSlotForGrandCompany(4u));
    }

    [Theory]
    [InlineData(1u, 1u)] // GLA -> GLA (already base)
    [InlineData(29u, 29u)] // ROG -> ROG (already base)
    public void BaseClassFor_BaseClasses_ReturnsSelf(uint classJobId, uint expected)
    {
        Assert.Equal(expected, HuntingSlotTable.BaseClassFor(classJobId));
    }

    // The controller-wave bug (HuntingLogService.cs:311): jobKey was built from the raw evolved
    // classJobId instead of this mapping, so every one of these ten jobs failed the dataset
    // lookup ("Hunting log data missing for this job.") even though HuntingSlotTable itself
    // resolved the slot correctly via EvolvedToBaseClass.
    [Theory]
    [InlineData(19u, 1u)] // PLD <- GLA
    [InlineData(20u, 2u)] // MNK <- PGL
    [InlineData(21u, 3u)] // WAR <- MRD
    [InlineData(22u, 4u)] // DRG <- LNC
    [InlineData(23u, 5u)] // BRD <- ARC
    [InlineData(24u, 6u)] // WHM <- CNJ
    [InlineData(25u, 7u)] // BLM <- THM
    [InlineData(27u, 26u)] // SMN <- ACN
    [InlineData(28u, 26u)] // SCH <- ACN
    [InlineData(30u, 29u)] // NIN <- ROG
    public void BaseClassFor_EvolvedJobs_ReturnsBaseClassId(uint evolvedClassJobId, uint expectedBaseClassJobId)
    {
        Assert.Equal(expectedBaseClassJobId, HuntingSlotTable.BaseClassFor(evolvedClassJobId));
    }

    [Fact]
    public void BaseClassFor_UnmappedJob_ReturnsSelf()
    {
        // Post-Stormblood jobs (and unknown ids) have no evolved->base mapping — BaseClassFor
        // degrades to identity rather than throwing; HuntingLogService only ever calls it once
        // SlotForClassJob has already confirmed a class log exists for this job.
        Assert.Equal(31u, HuntingSlotTable.BaseClassFor(31u));
    }
}
