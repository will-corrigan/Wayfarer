namespace Wayfarer.Routing;

/// <summary>The bearing maths behind the compass needle. FFXIV's axes are +X east and +Z south,
/// so north is -Z; a bearing here is 0 at north and clockwise-positive, and a screen angle is
/// that bearing turned by the camera so the needle points the way the player is looking.
///
/// <para>The camera's own yaw is counter-clockwise-positive from north (0 north, +π/2 west), so
/// turning a clockwise bearing into the camera's frame is an addition, not a subtraction. That sign was settled by a live four-direction
/// test on 2026-08-21 and matches the shipping Compass plugin's documented convention; do not flip
/// it without new evidence.</para></summary>
public static class Compass
{
    /// <summary>Bearing from an offset: 0 = north, clockwise positive, in radians.</summary>
    public static float Bearing(float dx, float dz) => MathF.Atan2(dx, -dz);

    /// <summary>The angle to draw the needle at, given a bearing and the camera's yaw.</summary>
    public static float NeedleAngle(float bearing, float cameraYaw) => Normalize(bearing + cameraYaw);

    /// <summary>Wraps an angle into [-π, π]. The remainder of a turn is what this is, and the
    /// runtime does it in one call and to the nearest whole turn rather than by walking one turn
    /// at a time.</summary>
    public static float Normalize(float angle) => float.Ieee754Remainder(angle, 2f * MathF.PI);
}
