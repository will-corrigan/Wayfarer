using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>One element's entry in the game's cursor-navigation graph:
/// <c>AtkCursorNavigationInfo</c>'s five bytes (self index plus the four neighbour indices).
/// Absolute addresses within a single addon's index space — never offsets, never pointers, and
/// never shared between two elements of the same addon.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NavLink(int Index, int Up, int Down, int Left, int Right);
