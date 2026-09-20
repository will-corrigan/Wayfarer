namespace Wayfarer.Guidance;

/// <summary>The sorts of thing a step can be about, in the order a step means them: something to
/// act on first, and whatever lives there second.</summary>
public enum MarkKind
{
    /// <summary>A thing placed in the world to be interacted with.</summary>
    Thing,

    /// <summary>A kind of creature that stands there.</summary>
    Creature,
}
