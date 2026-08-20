namespace Wayfarer.Core.Navigation;

public static class NavMath
{
    // In-game calibration knobs (Task 7 gate): if the arrow is consistently 180° off,
    // set CameraYawOffset = MathF.PI; if it rotates the wrong way when the camera
    // turns, set CameraYawSign = -1f.
    public const float CameraYawOffset = 0f;
    public const float CameraYawSign = 1f;

    /// <summary>Bearing to a target offset. FFXIV axes: +X east, +Z south, so north = -Z.
    /// 0 = north, clockwise positive.</summary>
    public static float Bearing(float dx, float dz) => MathF.Atan2(dx, -dz);

    /// <summary>Screen rotation for the arrow: 0 = straight up = run the way the camera faces.</summary>
    public static float ArrowAngle(float bearing, float cameraYaw) =>
        Normalize(bearing - ((CameraYawSign * cameraYaw) + CameraYawOffset));

    public static float Normalize(float a)
    {
        while (a > MathF.PI)
        {
            a -= 2 * MathF.PI;
        }

        while (a < -MathF.PI)
        {
            a += 2 * MathF.PI;
        }

        return a;
    }

    public static float Distance(float dx, float dy, float dz) =>
        MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

    public static string FormatDistance(float yalms) =>
        yalms >= 1000f ? $"{yalms / 1000f:0.0}k yalms" : $"{MathF.Round(yalms)} yalms";
}
