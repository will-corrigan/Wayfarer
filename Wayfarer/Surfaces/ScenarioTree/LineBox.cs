using System.Numerics;
using System.Runtime.InteropServices;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>A rectangle inside a line: where it starts and how big it is, in the line's own
/// coordinates. What a control is put over.</summary>
/// <param name="At">The top left corner.</param>
/// <param name="Size">How wide and how tall.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct LineBox(Vector2 At, Vector2 Size)
{
    /// <summary>The same box, moved across by the room kept in front of the words. The game
    /// measures within the text node; a control is placed within the line that holds it.</summary>
    public LineBox MovedBy(float across) => this with { At = At + new Vector2(across, 0f) };
}
