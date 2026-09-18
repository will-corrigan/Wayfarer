namespace Wayfarer.Core.Routing;

/// <summary>The bearing maths behind the compass needle. FFXIV's axes are +X east and +Z south,
/// so north is -Z; a bearing here is 0 at north and clockwise-positive, and a screen angle is
/// that bearing turned by the camera so the needle points the way the player is looking.
///
/// <para>The camera's own yaw is counter-clockwise-positive from north (0 north, +π/2 west), which
/// is why it is negated before it is subtracted. That sign was settled by a live four-direction
/// test on 2026-08-21 and matches the shipping Compass plugin's documented convention; do not flip
/// it without new evidence.</para></summary>
public static class Compass
{
    /// <summary>Bearing from an offset: 0 = north, clockwise positive, in radians.</summary>
    public static float Bearing(float dx, float dz) => MathF.Atan2(dx, -dz);

    /// <summary>The angle to draw the needle at, given a bearing and the camera's yaw.</summary>
    public static float NeedleAngle(float bearing, float cameraYaw) => Normalize(bearing + cameraYaw);

    /// <summary>Wraps an angle into (-π, π].</summary>
    public static float Normalize(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= 2f * MathF.PI;
        }

        while (angle < -MathF.PI)
        {
            angle += 2f * MathF.PI;
        }

        return angle;
    }
}
