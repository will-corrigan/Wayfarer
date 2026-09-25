using System.Numerics;
using Lumina.Data.Parsing.Layer;

namespace Wayfarer.RoutingGen;

/// <summary>A box, cylinder or sphere a zone's layout places to do something to whoever walks into
/// it: say which map they are on, or move them to another zone. Its scale is half its size on each
/// axis, and it is turned about the upright axis.</summary>
internal sealed record Trigger(TriggerBoxShape Shape, Vector3 Centre, float Turn, Vector3 Half)
{
    /// <summary>A layout's trigger, from where it is placed.</summary>
    public static Trigger Of(TriggerBoxShape shape, in LayerCommon.InstanceObject thing) => new(
        shape,
        new Vector3(thing.Transform.Translation.X, thing.Transform.Translation.Y, thing.Transform.Translation.Z),
        thing.Transform.Rotation.Y,
        new Vector3(thing.Transform.Scale.X, thing.Transform.Scale.Y, thing.Transform.Scale.Z));

    /// <summary>Whether a point is inside it.</summary>
    public bool Holds(Vector3 at)
    {
        var offset = at - Centre;
        switch (Shape)
        {
            case TriggerBoxShape.TriggerBoxShapeCylinder:
                return MathF.Abs(offset.Y) <= Half.Y && ((offset.X * offset.X) + (offset.Z * offset.Z)) <= Half.X * Half.X;
            case TriggerBoxShape.TriggerBoxShapeSphere:
                return offset.Length() <= Half.X;
            default:
                var (x, z) = Turned(offset.X, offset.Z, -Turn);
                return MathF.Abs(x) <= Half.X && MathF.Abs(offset.Y) <= Half.Y && MathF.Abs(z) <= Half.Z;
        }
    }

    /// <summary>The point of its footprint on the ground nearest a point: where someone walking
    /// from there first steps into it. The point itself when it is already inside.</summary>
    public Vector2 NearestAcross(Vector2 from)
    {
        var offset = from - new Vector2(Centre.X, Centre.Z);
        if (Shape is TriggerBoxShape.TriggerBoxShapeCylinder or TriggerBoxShape.TriggerBoxShapeSphere)
        {
            var far = offset.Length();
            return far <= Half.X ? from : new Vector2(Centre.X, Centre.Z) + (offset * (Half.X / far));
        }

        // Turned back so its sides line up with the axes, clamped inside them, and turned again.
        var (x, z) = Turned(offset.X, offset.Y, -Turn);
        var (nearX, nearZ) = Turned(Math.Clamp(x, -Half.X, Half.X), Math.Clamp(z, -Half.Z, Half.Z), Turn);
        return new Vector2(Centre.X + nearX, Centre.Z + nearZ);
    }

    private static (float X, float Z) Turned(float x, float z, float turn)
    {
        var (sin, cos) = MathF.SinCos(turn);
        return ((x * cos) - (z * sin), (x * sin) + (z * cos));
    }
}
