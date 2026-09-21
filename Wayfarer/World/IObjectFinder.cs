using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Wayfarer.Routing;

namespace Wayfarer.World;

/// <summary>Finds the thing a module is looking for among everything standing in the world.
///
/// <para>Shared, because more than one module will want it: a step that says search this ground,
/// a hunt that says kill that creature and a gathering node that comes and goes are all the same
/// question — of everything standing here, which one do I mean. What counts as wanted is the
/// module's own business and comes in as marks; finding it is not.</para></summary>
internal interface IObjectFinder
{
    /// <summary>The thing to go to inside an area, or null when none of what the module named is
    /// standing there.</summary>
    /// <param name="areas">The ground to search, all of it at once so the nearest answer wins
    /// rather than the first. A place with no room in it is not searched.</param>
    /// <param name="marks">What is being looked for, by the id the world gives it and its sort.</param>
    /// <param name="owner">The game event whose own spawns count, or null.</param>
    Found? Inside(IReadOnlyList<Place> areas, IReadOnlyList<Mark>? marks, EventId? owner);
}
