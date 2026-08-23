namespace Wayfarer.Core.Navigation;

public static class NavMath
{
    // Camera->DirH is counter-clockwise-positive from north (0 = north, +pi/2 = west).
    // Our bearing/screen frame is clockwise-positive from north, so CameraYawSign = -1
    // converts DirH into that frame (ArrowAngle reduces to bearing + cameraYaw).
    // Evidence: live four-direction test 2026-08-21 (error zero facing N/S, mirrored
    // E/W under the old Sign = +1) plus the shipping Compass plugin's documented
    // DirH convention. Do not flip this without new evidence.
    public const float CameraYawOffset = 0f;
    public const float CameraYawSign = -1f;

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

    /// <summary>The same direction the arrow points, in words. Used when the arrow image cannot be
    /// drawn at all — a readout that says "behind you, to the left" is still guidance, whereas an
    /// arrow that silently fails to render leaves the player with no direction at all. Eight
    /// sectors, because that is as fine as a phrase can usefully be.</summary>
    public static string DescribeDirection(float arrowRadians)
    {
        var sector = (int)MathF.Round(Normalize(arrowRadians) / (MathF.PI / 4f));
        return (((sector % 8) + 8) % 8) switch
        {
            1 => "Ahead and to the right",
            2 => "To your right",
            3 => "Behind you, to the right",
            4 => "Behind you",
            5 => "Behind you, to the left",
            6 => "To your left",
            7 => "Ahead and to the left",
            _ => "Straight ahead",
        };
    }

    public static string FormatDistance(float yalms) =>
        yalms >= 1000f ? $"{yalms / 1000f:0.0}k yalms" : $"{MathF.Round(yalms)} yalms";
}
