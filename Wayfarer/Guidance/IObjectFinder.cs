using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Finds the thing a step is about among the objects standing in the world. Behind an
/// interface so the guidance that uses it can be reasoned about, and tested, without the game.</summary>
internal interface IObjectFinder
{
    /// <summary>The wanted things standing inside an area: the nearest of them to the player, and
    /// how many there are. Null when none is there. Only an area has an answer: a place with no
    /// room in it is already the thing.</summary>
    /// <param name="area">The circle being searched, with its radius.</param>
    /// <param name="marks">Ids the caller listed as what to look for, or null.</param>
    /// <param name="owner">The game event whose own objects count, or null.</param>
    Found? Inside(Place area, IReadOnlyList<uint>? marks, uint? owner);
}
