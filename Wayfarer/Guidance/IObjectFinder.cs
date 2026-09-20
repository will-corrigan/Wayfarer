using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Finds the thing a step is about among the objects standing in the world. Behind an
/// interface so the guidance that uses it can be reasoned about, and tested, without the game.</summary>
internal interface IObjectFinder
{
    /// <summary>The nearest wanted thing standing inside an area, or null when none is there.
    /// Only an area has an answer: a place with no room in it is already the thing.</summary>
    /// <param name="area">The circle being searched, with its radius.</param>
    /// <param name="marks">Ids the caller listed as what to look for, or null.</param>
    /// <param name="owner">The game event whose own objects count, or null.</param>
    /// <param name="expected">Where they are known to stand when none is there, or null.</param>
    Place? Inside(Place area, IReadOnlyList<Mark>? marks, EventId? owner, IReadOnlyList<Place>? expected);
}
