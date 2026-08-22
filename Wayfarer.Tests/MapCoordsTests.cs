using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

public class MapCoordsTests
{
    [Theory]
    [InlineData(27f, 24f, 100u, 0, 0)] // real Map#20 (La Noscea) values — see MapCoords.MapToWorld doc
    [InlineData(1f, 1f, 100u, 0, 0)] // map-coord minimum
    [InlineData(42f, 42f, 100u, 0, 0)] // map-coord near maximum
    [InlineData(21.5f, 21.5f, 200u, 0, 0)] // zoomed-in map (higher SizeFactor)
    [InlineData(15f, 30f, 100u, -10, 5)] // non-zero offsets
    [InlineData(15f, 30f, 400u, 12, -8)] // zoomed map with non-zero offsets
    public void MapToWorld_RoundTripsThroughWorldToMap(float mapX, float mapY, uint sizeFactor, int offsetX, int offsetY)
    {
        var (worldX, worldZ) = MapCoords.MapToWorld(mapX, mapY, sizeFactor, offsetX, offsetY);

        var backX = MapCoords.WorldToMapAxis(worldX, sizeFactor, offsetX);
        var backY = MapCoords.WorldToMapAxis(worldZ, sizeFactor, offsetY);

        Assert.Equal(mapX, backX, 3);
        Assert.Equal(mapY, backY, 3);
    }

    [Fact]
    public void MapToWorld_KnownValue_MatchesLiveMapSheetCheck()
    {
        // Gladiator 01's primary location (data/hunting-targets.json: territoryTypeId 140, mapId
        // 20, x=27, y=24) against Map#20's real sheet row (SizeFactor 100, OffsetX/Y 0) — cross
        // -checked against a live Lumina read 2026-08-22 (world ~= (276, 126), round-trips exact).
        var (worldX, worldZ) = MapCoords.MapToWorld(27f, 24f, 100u, 0, 0);

        Assert.Equal(276f, worldX, 1);
        Assert.Equal(126f, worldZ, 1);
    }
}
