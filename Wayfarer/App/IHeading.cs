namespace Wayfarer.App;

/// <summary>The two numbers that change every frame the player moves or turns, for the surfaces
/// that draw them: the compass needle and the distance line. Read on the surface's own draw, not
/// published, because they would change every frame and there is nothing to lay out for.</summary>
public interface IHeading
{
    /// <summary>The needle's angle on screen, in radians, toward the current route's end; null
    /// when there is no route.</summary>
    float? Needle { get; }

    /// <summary>Straight-line distance to the current route's end, in yalms; null when there is no
    /// route.</summary>
    float? DistanceYalms { get; }

    /// <summary>How far above the player the next walk ends, in yalms, negative when below, or
    /// null when there is nothing to walk to.</summary>
    float? RiseYalms { get; }
}
