using System.Runtime.InteropServices;
using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>What a search of an area turned up: the one being guided to, and how many of them are
/// standing there. The count matters because a search area often holds several of the same thing
/// and only one of them is the one — the player has to try them, so being told there are more is
/// the difference between looking and giving up.</summary>
/// <param name="At">The nearest one to the player.</param>
/// <param name="Count">How many were found, never less than one.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct Found(Place At, int Count);
