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
}
