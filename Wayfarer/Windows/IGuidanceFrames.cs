using Wayfarer.Windows.Native;

namespace Wayfarer.Windows;

/// <summary>What the host draws this frame. The seam between the game's addon and the rest of the
/// plugin: the host asks for a frame on every update and knows nothing about where it came from.
/// </summary>
internal interface IGuidanceFrames
{
    /// <summary>This frame's content and bearing, or null when nothing should be drawn.</summary>
    ReadoutFrame? Next();
}
