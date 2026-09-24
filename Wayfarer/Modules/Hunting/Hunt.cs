namespace Wayfarer.Modules.Hunting;

/// <summary>A hunt the player chose to follow: a page of a hunting log, or a mark bill.
///
/// <para>A page is the game's own way of naming one: the log it belongs to and its rank. The log
/// is named the way the game numbers its rows, a class's or Grand Company's id multiplied by the
/// base the game counts that sort of log from, so a class and a company can never be taken for
/// each other. The slot is where the game keeps that log's kills.</para>
///
/// <para>A bill is named by which of the game's twenty-two kinds of bill it is, and by the very
/// bill held when it was followed: once that one is handed in, a new one of the same kind is a
/// different bill, and following the old one is over.</para></summary>
/// <param name="Kind">Which sort of hunt this is.</param>
/// <param name="Log">For a page, the log's id times its base; for a bill, the kind of bill.</param>
/// <param name="Rank">For a page, its rank, counted from zero.</param>
/// <param name="Slot">For a page, where the game keeps that log's kills.</param>
/// <param name="Order">For a bill, the bill held when it was followed.</param>
internal sealed record Hunt(HuntKind Kind, uint Log, byte Rank = 0, byte Slot = 0, uint Order = 0)
{
    /// <summary>A page of a hunting log.</summary>
    public static Hunt Page(uint log, byte rank, byte slot) => new(HuntKind.LogPage, log, rank, slot);

    /// <summary>A mark bill.</summary>
    public static Hunt Bill(byte markIndex, uint order) => new(HuntKind.Bill, markIndex, Order: order);
}
