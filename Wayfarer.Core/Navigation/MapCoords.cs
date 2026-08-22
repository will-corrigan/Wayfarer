namespace Wayfarer.Core.Navigation;

public static class MapCoords
{
    /// <summary>Converts MapMarker sheet pixel coords (0–2048, 1024 = map center)
    /// to world coords, inverting: pixel = (world + offset) * (sizeFactor/100) + 1024.
    /// Used only to compare relative distances within one map, so a uniform offset
    /// error would cancel out — but the formula is the standard one regardless.</summary>
    public static (float X, float Z) MarkerPixelToWorld(
        short pixelX, short pixelY, ushort sizeFactor, short offsetX, short offsetY)
    {
        var c = sizeFactor / 100f;
        return (((pixelX - 1024f) / c) - offsetX, ((pixelY - 1024f) / c) - offsetY);
    }

    /// <summary>Converts a "map coordinate" (the ~1.0-42.0 human-readable scale shown on the
    /// map/minimap and used by flags/<c>MapLinkPayload</c> — e.g. the hunting-log data file's
    /// x/y) to a world position, by inverting Dalamud's own
    /// <c>MapUtil.ConvertWorldCoordXZToMapCoord</c>:
    /// <c>worldToMap(v) = 0.02*offset + 2048/scale + 0.02*v + 1.0</c>, so algebraically
    /// <c>mapToWorld(m) = (m - 1.0 - 0.02*offset - 2048/scale) / 0.02</c>. Applied independently
    /// to X/Z using the target's <c>Map</c> sheet row (<paramref name="sizeFactor"/> = the raw
    /// <c>SizeFactor</c> field, NOT divided by 100 — unlike <see cref="MarkerPixelToWorld"/>'s
    /// unrelated pixel-space formula). Round-tripped against the forward formula both
    /// algebraically (<c>MapCoordsTests</c>) and against live <c>Map</c> sheet data (SizeFactor
    /// 100, offsets 0, Gladiator 01's primary location map(27,24) → world → map(27,24) exact)
    /// 2026-08-22. World Y (vertical) cannot be recovered from a map coordinate — the game drops
    /// it for map purposes; callers must source it separately (e.g. IObjectTable/terrain).</summary>
    public static (float X, float Z) MapToWorld(
        float mapX, float mapY, uint sizeFactor, int offsetX, int offsetY) =>
        (MapToWorldAxis(mapX, sizeFactor, offsetX), MapToWorldAxis(mapY, sizeFactor, offsetY));

    /// <summary>Forward direction — Dalamud's <c>MapUtil.ConvertWorldCoordXZToMapCoord</c>,
    /// reproduced here only so <see cref="MapToWorld"/> can be round-trip-tested against it;
    /// Wayfarer's router only ever needs the inverse.</summary>
    public static float WorldToMapAxis(float worldCoord, uint sizeFactor, int offset) =>
        (0.02f * offset) + (2048f / sizeFactor) + (0.02f * worldCoord) + 1.0f;

    private static float MapToWorldAxis(float mapCoord, uint sizeFactor, int offset) =>
        (mapCoord - 1f - (0.02f * offset) - (2048f / sizeFactor)) / 0.02f;
}
