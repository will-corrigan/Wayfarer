using Wayfarer.Core.Hunting;

namespace Wayfarer.Tests;

public class HuntingChainingTests
{
    [Fact]
    public void OrderNearestFirst_ChainsFromPlayerPosition_NearestFirst()
    {
        var far = Target(bNpcNameId: 1, x: 100f, z: 0f);
        var near = Target(bNpcNameId: 2, x: 10f, z: 0f);
        var mid = Target(bNpcNameId: 3, x: 40f, z: 0f);

        var ordered = HuntingChaining.OrderNearestFirst([far, near, mid], currentTerritory: 1, px: 0f, pz: 0f);

        Assert.Equal([2u, 3u, 1u], ordered.Select(t => t.Monster.BNpcNameId));
    }

    [Fact]
    public void OrderNearestFirst_HopsFromLastVisitedTarget_NotOriginalPlayerPosition()
    {
        // Player starts at 0,0. Measured from the origin alone, order would be a(10) < c(11) <
        // b(19). But once the chain reaches a (x=10), b (x=19, dist 9 from a) is now closer than
        // c (x=-11, dist 21 from a) — proves the chain re-anchors on each hop rather than always
        // measuring from px/pz.
        var a = Target(1, x: 10f, z: 0f);
        var b = Target(2, x: 19f, z: 0f);
        var c = Target(3, x: -11f, z: 0f);

        var ordered = HuntingChaining.OrderNearestFirst([c, b, a], currentTerritory: 1, px: 0f, pz: 0f);

        Assert.Equal([1u, 2u, 3u], ordered.Select(t => t.Monster.BNpcNameId));
    }

    [Fact]
    public void OrderNearestFirst_DropsTargetsOutsideCurrentTerritory()
    {
        var here = Target(1, x: 5f, z: 0f, territory: 100);
        var elsewhere = Target(2, x: 1f, z: 0f, territory: 200);

        var ordered = HuntingChaining.OrderNearestFirst([here, elsewhere], currentTerritory: 100, px: 0f, pz: 0f);

        var only = Assert.Single(ordered);
        Assert.Equal(1u, only.Monster.BNpcNameId);
    }

    [Fact]
    public void OrderNearestFirst_NoTargetsInZone_ReturnsEmpty()
    {
        var elsewhere = Target(1, x: 1f, z: 0f, territory: 200);

        var ordered = HuntingChaining.OrderNearestFirst([elsewhere], currentTerritory: 100, px: 0f, pz: 0f);

        Assert.Empty(ordered);
    }

    private static HuntingChainTarget Target(uint bNpcNameId, float x, float z, uint territory = 1) =>
        new(new HuntingMonster { BNpcNameId = bNpcNameId }, territory, x, z);
}
