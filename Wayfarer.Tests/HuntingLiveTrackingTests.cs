using Wayfarer.Core.Hunting;

namespace Wayfarer.Tests;

public class HuntingLiveTrackingTests
{
    [Fact]
    public void IsCandidate_MatchingNameIdAliveTargetable_IsTrue()
    {
        Assert.True(HuntingLiveTracking.IsCandidate(2058, 2058, isDead: false, isTargetable: true));
    }

    [Fact]
    public void IsCandidate_DifferentNameId_IsFalse()
    {
        // The regression this seam exists for: a BNpcBase row id (what IGameObject.BaseId/DataId
        // carries for battle NPCs) is a different id space from the dataset's BNpcName ids —
        // callers must pass IBattleNpc.NameId, and any other id must simply not match.
        Assert.False(HuntingLiveTracking.IsCandidate(295, 2058, isDead: false, isTargetable: true));
    }

    [Fact]
    public void IsCandidate_DeadObject_IsFalse()
    {
        Assert.False(HuntingLiveTracking.IsCandidate(2058, 2058, isDead: true, isTargetable: true));
    }

    [Fact]
    public void IsCandidate_UntargetableObject_IsFalse()
    {
        // Spawn-animation or event clones can carry the right NameId while untargetable — they
        // must not win "nearest".
        Assert.False(HuntingLiveTracking.IsCandidate(2058, 2058, isDead: false, isTargetable: false));
    }
}
