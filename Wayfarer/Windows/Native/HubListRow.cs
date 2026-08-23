using System.Numerics;

namespace Wayfarer.Windows.Native;

/// <summary>One hub list row's data. Mutable in the fields that change without the list itself
/// changing (distance captions), so a per-tick <c>ListNode.Update()</c> can refresh them without
/// rebuilding the list under the player's cursor.</summary>
internal sealed class HubListRow
{
    public required HubRowKind Kind { get; init; }

    public required string Label { get; init; }

    /// <summary>Line two: what this entry actually is, in the player's own register. Every unlock
    /// in the catalogue carries one of these and the window drew none of them — which is the whole
    /// of "I don't know what half of the things are or do". Empty on headings, and on notes, whose
    /// own text wraps into this line's space instead.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Right-aligned trailing text on line one: a level and a zone, a kill count, a
    /// distance. Two short tokens at most — three facts in this space is what made it unreadable.
    /// Kept on the row rather than wrapped underneath so every row is the same height, which is
    /// what lets the list virtualize (and therefore what lets it carry controller navigation).</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The game icon in the row's left column — the state's shape. Already validated by
    /// <see cref="HubStatusIcons"/>: 0 means "there is no icon to draw", and the row says its state
    /// in <see cref="StatusWord"/> instead rather than leaving a hole where the state should be.</summary>
    public uint IconId { get; set; }

    /// <summary>The state in one word, used only when <see cref="IconId"/> is 0. Colour is never
    /// the only signal and neither is a shape that did not load.</summary>
    public string StatusWord { get; init; } = string.Empty;

    /// <summary>Overrides the kind's default colour — used for per-status unlock colouring.</summary>
    public Vector4? LabelColor { get; init; }

    /// <summary>Invoked on mouse click and on controller confirm alike. Null on headings and
    /// notes, which are still focusable (the game's own lists behave the same way) but inert.</summary>
    public Action? Activate { get; init; }
}
