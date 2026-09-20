using System.Runtime.InteropServices;

namespace Wayfarer.Modules.Quests;

/// <summary>Where a row's icons go when the game's own and ours are laid out together.
///
/// <para>A row keeps a strip for the icons it may need to show — level sync, unrestricted party,
/// and the rest — and most rows light only one or two of them. So a mark of ours does not have to
/// be squeezed in beside the strip or taken out of the name: the icons on show, the game's and
/// ours alike, are laid out in the space the game already keeps, ours first and the game's kept in
/// their own order against the right.</para>
///
/// <para>Only when there are more icons than that space holds does anything have to give, and then
/// the layout says by how much rather than deciding for itself. The row's name is what gives, which
/// is the same thing every other plugin marking these rows does, so marks still sit side by side
/// rather than on top of each other.</para></summary>
internal static class StripLayout
{
    /// <summary>Lays out a row's icons.</summary>
    /// <param name="icons">How many are to be shown, ours and the game's together.</param>
    /// <param name="left">Where the game's strip begins.</param>
    /// <param name="right">Where it ends.</param>
    /// <param name="pitch">How far apart icons sit, which is one icon's width.</param>
    /// <returns>Where the leftmost icon goes, and how much room was wanted beyond the strip.</returns>
    public static Strip Place(int icons, float left, float right, float pitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(icons);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pitch);
        if (icons == 0)
        {
            return new Strip(right, 0f, pitch);
        }

        // Against the right, so the game's own icons stay where the player is used to seeing them
        // and ours appear on the inside edge, nearest the name.
        var first = right - (icons * pitch);
        return new Strip(first, MathF.Max(0f, left - first), pitch);
    }

    /// <summary>Where a row's icons sit.</summary>
    /// <param name="First">Where the leftmost icon goes.</param>
    /// <param name="Overflow">How much room was wanted beyond the strip's left edge, which is what
    /// the row's name has to give up for everything to fit. Zero when it all fits.</param>
    /// <param name="Pitch">How far apart the icons sit.</param>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Strip(float First, float Overflow, float Pitch)
    {
        /// <summary>Where the icon at this place in the row sits, counted from the left.</summary>
        public float At(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            return First + (index * Pitch);
        }
    }
}
