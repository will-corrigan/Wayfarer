using Lumina;
using Lumina.Excel;

namespace Wayfarer.SheetReader;

/// <summary>Searches the game's sheets as bytes rather than through the names somebody has given
/// them.
///
/// <para>Lumina reads a sheet through a generated class, one per sheet, whose columns are named
/// where the community has worked out what they mean. Where it has not, the sheet either has no
/// class at all or has one whose columns are called Unknown -- and a search that goes through those
/// classes cannot see a column nobody has named yet. That is not the data missing. That is the
/// search being blind, and it reads exactly the same from outside.</para>
///
/// <para>So this walks every sheet the game defines, by name, and reads every column by number.
/// Nothing is skipped for want of a schema: an answer sitting in an unnamed column of an unmapped
/// sheet is found here and was invisible before.</para></summary>
internal static class Raw
{
    /// <summary>Every row of every sheet holding the wanted number, in any column.</summary>
    /// <param name="game">The game's data.</param>
    /// <param name="wanted">The number to look for.</param>
    /// <param name="only">Substring a sheet's name must contain, or null for all of them.</param>
    public static void Find(GameData game, uint wanted, string? only)
    {
        var names = game.Excel.SheetNames
            .Where(n => only is null || n.Contains(only, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"looking for {wanted} in every column of {names.Count} sheets");
        var (hits, read, unread) = (0, 0, new List<string>());

        foreach (var name in names)
        {
            ExcelSheet<RawRow>? sheet;
            try
            {
                sheet = game.Excel.GetSheet<RawRow>(null, name);
            }
            catch (Exception)
            {
                // Subrow sheets are read a different way; a sheet that is neither is one Lumina
                // cannot open at all, and is reported rather than passed over in silence.
                try
                {
                    foreach (var subrows in game.Excel.GetSubrowSheet<RawSubrow>(null, name))
                    {
                        foreach (var row in subrows)
                        {
                            hits += Look(name, row.RowId, row.Columns.Count, i => row.ReadColumn(i), wanted);
                        }
                    }

                    read++;
                }
                catch (Exception ex)
                {
                    unread.Add($"{name} ({ex.GetType().Name})");
                }

                continue;
            }

            try
            {
                foreach (var row in sheet)
                {
                    hits += Look(name, row.RowId, row.Columns.Count, i => row.ReadColumn(i), wanted);
                }

                read++;
            }
            catch (Exception ex)
            {
                unread.Add($"{name} ({ex.GetType().Name})");
            }
        }

        Console.WriteLine($"{hits} references across {read} sheets, {unread.Count} unreadable");
        if (unread.Count > 0)
        {
            Console.WriteLine($"  unreadable: {string.Join(", ", unread)}");
        }
    }

    private static int Look(string sheet, uint id, int columns, Func<int, object> read, uint wanted)
    {
        var hits = 0;
        for (var i = 0; i < columns; i++)
        {
            object value;
            try
            {
                value = read(i);
            }
            catch (Exception)
            {
                continue;
            }

            if (Is(value, wanted))
            {
                Console.WriteLine($"  {sheet}[{id}] column {i} = {wanted}");
                hits++;
            }
        }

        return hits;
    }

    private static bool Is(object value, uint wanted) => value switch
    {
        uint n => n == wanted,
        int n => n >= 0 && (uint)n == wanted,
        ushort n => n == wanted,
        short n => n >= 0 && (uint)n == wanted,
        _ => false,
    };
}
