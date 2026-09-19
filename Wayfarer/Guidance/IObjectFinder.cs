using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Finds the thing a step is about among the objects standing in the world. Behind an
/// interface so the guidance that uses it can be reasoned about, and tested, without the game.</summary>
internal interface IObjectFinder
{
    /// <summary>Where the nearest wanted thing is standing inside an area, or null when none of
    /// them is there. Only an area has an answer: a place with no room in it is already the thing.</summary>
    /// <param name="area">The circle being searched, with its radius.</param>
    /// <param name="marks">Ids the caller listed as what to look for, or null.</param>
    /// <param name="owner">The game event whose own objects count, or null.</param>
    Place? Inside(Place area, IReadOnlyList<uint>? marks, uint? owner);
}
