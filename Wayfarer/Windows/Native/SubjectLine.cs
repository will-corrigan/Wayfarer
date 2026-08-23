using System.Runtime.InteropServices;

namespace Wayfarer.Windows.Native;

/// <summary>Where the line that names what is being followed ended up, so the controls that belong
/// to it can be parked against the words rather than against the readout's invisible box.
///
/// <para>The readout is a fixed 320 units wide and its quest name is usually a fraction of that, so
/// a control pinned to the right-hand edge would float in empty space with nothing to belong to.
/// Everything here is a measurement taken during the layout pass that produced the line.</para></summary>
/// <param name="Top">The line's own top edge, in the readout's units.</param>
/// <param name="Height">How tall the line turned out.</param>
/// <param name="FontSize">The size the line was drawn at, which is what a control beside it is
/// centred against.</param>
/// <param name="TextWidth">How wide the words actually drew, already clamped to the room the line
/// was given.</param>
/// <param name="Truncated">The name did not fit and the engine cut it short with an ellipsis.
/// Decided by measuring the untruncated words against the room the line has, rather than by reading
/// the node back — the node reports whatever it last drew, and the answer is wanted on the frame the
/// name changes.</param>
/// <remarks>Never marshalled — the layout kind is explicit only because the analyzer asks every
/// all-blittable struct to say so, and Auto is what "this is a value, not a memory shape" means.</remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SubjectLine(
    float Top, float Height, float FontSize, float TextWidth, bool Truncated);
