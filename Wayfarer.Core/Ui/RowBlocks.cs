using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>Where every part of a list row goes. An empty rectangle means the part is not drawn.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct RowBlocks(
    ScreenRect Icon,
    ScreenRect Label,
    ScreenRect Trailing,
    ScreenRect Description)
{
    /// <summary>Every part, for a caller that wants to check them all at once.</summary>
    public IEnumerable<ScreenRect> Blocks => [Icon, Label, Trailing, Description];
}
