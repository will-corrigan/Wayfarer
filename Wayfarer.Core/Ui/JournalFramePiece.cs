using System.Runtime.InteropServices;

namespace Wayfarer.Core.Ui;

/// <summary>One of the sixteen nodes the Journal's gilt border is assembled from: where it goes on
/// the page, which rectangle of <c>ui/uld/Journal_Frame.tex</c> it samples, whether it is mirrored,
/// and whether it stretches.
///
/// <para><b>Why the source rectangle travels with the destination.</b> An image node samples a
/// rectangle out of a sheet; give it the right origin and the wrong size and it draws a band of
/// nothing, which is exactly how an unexamined crop ships as a smear. Keeping the pair together
/// means a piece cannot be placed without saying what it is made of.</para></summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct JournalFramePiece(

    /// <summary>The node id this piece reproduces, from JournalDetail's frame group <c>#11</c>.
    /// Carried so a rendered frame can be checked against the game's own tree piece by piece.
    /// </summary>
    int SourceNode,

    /// <summary>Where the piece is drawn, in the frame's own space.</summary>
    ScreenRect Destination,

    /// <summary>The rectangle of <c>Journal_Frame.tex</c> it samples — part list 10's own part.
    /// </summary>
    ScreenRect Source,

    /// <summary>Whether the art is mirrored left-to-right. Five of the sixteen are: the game draws
    /// one corner, one edge run and one bar and flips them for the other side.</summary>
    bool FlipHorizontally,

    /// <summary>Whether the piece is a nine-grid that stretches to its destination height. Only the
    /// two vertical rails are — every other piece is drawn at the size its art is authored at.
    /// </summary>
    bool Stretches)
{
    /// <summary>Whether the piece has anything to draw. A rail goes empty at the frame's minimum
    /// height, and an empty piece must be hidden rather than drawn at zero.</summary>
    public bool IsEmpty => Destination.IsEmpty;
}
