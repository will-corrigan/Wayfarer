using System.Runtime.InteropServices;

namespace Wayfarer.World;

/// <summary>Something a step is about, by the id the world gives it, and which sort of thing it is.
///
/// <para>The sort matters because a step is usually about one of them and the other only turns up
/// because of it. A circle to search holds the thing to act on, and once acted on it holds whatever
/// that summoned; guiding to the nearest of either sends the player to a beast when what they want
/// is the thing that called it.</para></summary>
/// <param name="Id">The id the world gives it: an object's, or a kind of creature's.</param>
/// <param name="Kind">Which sort it is.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Mark(uint Id, MarkKind Kind);
