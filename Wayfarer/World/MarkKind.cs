namespace Wayfarer.World;

/// <summary>The sorts of thing a step can be about, in the order a step means them.
///
/// <para>The order is what makes it useful: a thing placed to be used is what a step names when it
/// means one particular thing, a person is named from a list of everyone the errand involves, and
/// whatever lives there is only ever there because of one of the other two. So the more exactly
/// named is looked for first, and a broad list can only ever answer where nothing narrower
/// did.</para></summary>
public enum MarkKind
{
    /// <summary>A thing placed in the world to be interacted with, named one at a time.</summary>
    Thing,

    /// <summary>Someone the errand involves. Named from the whole cast of it, so many of them are
    /// nothing to do with the step in hand and only being there makes one worth guiding to.</summary>
    Person,

    /// <summary>A kind of creature that stands there.</summary>
    Creature,
}
