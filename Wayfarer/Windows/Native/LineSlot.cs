using System.Runtime.InteropServices;

namespace Wayfarer.Windows.Native;

/// <summary>Where one of the readout's subordinate lines put its parts, <b>inside its own section</b>.
///
/// <para>Everything here is an offset from the section's top edge, never a position on the readout.
/// That is the point: the two things that hang off a line without being in it — the arrow in the
/// gutter and the teleport click target — are parked by adding the section's own placed
/// <see cref="KamiToolKit.BaseTypes.NodeBase.Y"/> to one of these, which is a read of where the flow
/// put a container rather than a measurement of any text. The old code kept the same numbers as
/// absolute positions off a running cursor, and a cursor is what carried a bad measurement of one
/// line into the position of every line after it.</para></summary>
/// <param name="Index">Which slot of the pooled line nodes, and therefore which section.</param>
/// <param name="RuleAdvance">How much of the section is spent above the words: the rule and its
/// breathing room, or nothing at all when the line is not separated.</param>
/// <param name="TextTop">Where the words start in the section — after the rule, and centred in the
/// line's block unless the line wrapped.</param>
/// <param name="Height">The block the line's words occupy, not counting
/// <paramref name="RuleAdvance"/>.</param>
/// <param name="FontSize">The face the line was drawn at, which is what the arrow's optical centre is
/// a fraction of.</param>
/// <param name="Left">The line's own left edge.</param>
/// <param name="Width">The room the line's words had.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct LineSlot(
    int Index,
    float RuleAdvance,
    float TextTop,
    float Height,
    float FontSize,
    float Left,
    float Width);
