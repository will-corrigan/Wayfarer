namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>A stretch of the line, and the rectangle every stretch together fits inside. Empty
/// until the first stretch is taken in, so an empty line measures nothing rather than a point
/// at the origin.</summary>
internal readonly record struct Rect(float Left, float Top, float Width, float Height, bool Any = true)
{
    public static Rect Empty => new(0f, 0f, 0f, 0f, false);

    /// <summary>The smallest rectangle holding both. Every edge is taken from whichever
    /// reaches furthest, the bottom included: a stretch that wrapped to a second line makes the
    /// whole thing two lines tall, which is what keeps the line under it clear of its words.</summary>
    public Rect Union(Rect other)
    {
        if (!other.Any)
        {
            return this;
        }

        if (!Any)
        {
            return other;
        }

        var left = MathF.Min(Left, other.Left);
        var top = MathF.Min(Top, other.Top);
        var right = MathF.Max(Left + Width, other.Left + other.Width);
        var bottom = MathF.Max(Top + Height, other.Top + other.Height);
        return new Rect(left, top, right - left, bottom - top);
    }
}
