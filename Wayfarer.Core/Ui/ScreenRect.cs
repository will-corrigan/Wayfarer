using System.Numerics;
using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>A rectangle in raw screen pixels — a HUD element the readout has to keep clear of, or
/// the readout itself. Deliberately a plain value type in Core so the placement rules can be tested
/// without a game attached; the live addon reads that fill these in stay in the plugin.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ScreenRect(float X, float Y, float Width, float Height)
{
    public ScreenRect(Vector2 position, Vector2 size)
        : this(position.X, position.Y, size.X, size.Y)
    {
    }

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public bool Overlaps(ScreenRect other) =>
        !IsEmpty
        && !other.IsEmpty
        && X < other.Right
        && Right > other.X
        && Y < other.Bottom
        && Bottom > other.Y;
}
