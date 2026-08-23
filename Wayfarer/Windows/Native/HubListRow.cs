using System.Numerics;

namespace Wayfarer.Windows.Native;

/// <summary>One hub list row's data. Mutable in the fields that change without the list itself
/// changing (distance captions), so a per-tick <c>ListNode.Update()</c> can refresh them without
/// rebuilding the list under the player's cursor.</summary>
internal sealed class HubListRow
{
    public required HubRowKind Kind { get; init; }

    public required string Label { get; init; }

    /// <summary>Right-aligned trailing text: a distance, a kill count, a state word. Kept on the
    /// same line rather than wrapped underneath so every row is the same height, which is what
    /// lets the list virtualize (and therefore what lets it carry controller navigation).</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Overrides the kind's default colour — used for per-status unlock colouring.</summary>
    public Vector4? LabelColor { get; init; }

    /// <summary>Invoked on mouse click and on controller confirm alike. Null on headings and
    /// notes, which are still focusable (the game's own lists behave the same way) but inert.</summary>
    public Action? Activate { get; init; }
}
