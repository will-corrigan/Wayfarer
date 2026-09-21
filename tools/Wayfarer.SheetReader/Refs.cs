using System.Collections;
using System.Reflection;
using Lumina;
using Lumina.Excel;

namespace Wayfarer.SheetReader;

/// <summary>Every row in every sheet that points at a given row.
///
/// <para>Asking "which sheet says how you get this item" one sheet at a time is guesswork, and
/// guessing wrong reads exactly like the answer not existing — which is how a search stops early
/// and concludes the game does not know something it does know. This asks all of them.</para>
///
/// <para>A sheet is read through reflection because Lumina types them one class per sheet and there
/// are several hundred; every read is guarded because a handful of sheets have columns Lumina
/// cannot resolve, and one of those must not end the search.</para></summary>
internal static class Refs
{
    /// <summary>Finds every row that carries the wanted id, in any column, in any sheet.</summary>
    /// <param name="game">The game's data.</param>
    /// <param name="wanted">The row id being looked for.</param>
    /// <param name="only">Substring a sheet's name must contain, or null for all of them.</param>
    public static void Find(GameData game, uint wanted, string? only)
    {
        var sheets = typeof(Lumina.Excel.Sheets.Addon).Assembly
            .GetTypes()
            // Nested types are a sheet's own column groups, not sheets. They read as unreadable
            // and leaving them in made the count of what could not be searched look alarming when
            // every one of them was already being read inside its parent.
            .Where(t => t.Namespace == "Lumina.Excel.Sheets" && t.IsValueType && !t.IsEnum && !t.IsNested)
            .Where(t => only is null || t.Name.Contains(only, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"looking for {wanted} across {sheets.Count} sheets");
        var hits = 0;
        var unread = new List<string>();

        foreach (var type in sheets)
        {
            IEnumerable rows;
            try
            {
                rows = Rows(game, type);
            }
            catch (Exception ex)
            {
                unread.Add($"{type.Name} ({ex.GetType().Name})");
                continue;
            }

            var columns = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name is not ("ExcelPage" or "RowOffset" or "RowId" or "SubrowId"))
                .ToList();
            var id = type.GetProperty("RowId");
            if (id is null)
            {
                continue;
            }

            try
            {
                foreach (var row in rows)
                {
                    foreach (var column in columns)
                    {
                        if (Carries(column, row, wanted) is not { } where)
                        {
                            continue;
                        }

                        Console.WriteLine($"  {type.Name}[{id.GetValue(row)}].{column.Name} = {where}");
                        hits++;
                    }
                }
            }
            catch (Exception ex)
            {
                unread.Add($"{type.Name} ({ex.GetType().Name})");
            }
        }

        Console.WriteLine($"{hits} references, {unread.Count} sheets unreadable");
        if (unread.Count > 0)
        {
            // Named rather than counted. A sheet that could not be read is a place the answer
            // might be, and "not found" means nothing while that list is a number.
            Console.WriteLine($"  unreadable: {string.Join(", ", unread)}");
        }
    }

    /// <summary>Every row of a sheet, whichever of the two kinds it is. Some sheets hold several
    /// rows under one id -- a shop's list of what it sells is one of them -- and Lumina reads those
    /// through a different call. Reading them all as the plain kind is what had two hundred sheets
    /// counted as unreadable, shops among them.</summary>
    private static IEnumerable Rows(GameData game, Type type)
    {
        var subrow = type.GetInterfaces().Any(i => i.Name.StartsWith("IExcelSubrow", StringComparison.Ordinal));
        var name = subrow ? "GetSubrowSheet" : "GetSheet";

        var get = typeof(ExcelModule).GetMethods()
            .First(m => m.Name == name && m.GetParameters().Length == 2)
            .MakeGenericMethod(type);
        var sheet = get.Invoke(game.Excel, [null, null])!;

        if (!subrow)
        {
            return (IEnumerable)sheet;
        }

        // A subrow sheet enumerates collections of rows rather than rows, so it is flattened.
        return ((IEnumerable)sheet).Cast<IEnumerable>().SelectMany(group => group.Cast<object>());
    }

    /// <summary>What the column holds, if it holds the wanted id: the id itself for a plain number,
    /// the reference for a pointer, or the place in a list for a column holding several.</summary>
    private static string? Carries(PropertyInfo column, object row, uint wanted)
    {
        object? value;
        try
        {
            value = column.GetValue(row);
        }
        catch (Exception)
        {
            return null;
        }

        if (Points(value, wanted))
        {
            return $"->{wanted}";
        }

        if (value is IEnumerable list and not string)
        {
            var at = 0;
            foreach (var one in list)
            {
                if (Points(one, wanted))
                {
                    return $"[{at}] -> {wanted}";
                }

                at++;
            }
        }

        return null;
    }

    /// <summary>Whether one value is the wanted row: a reference to it, or a number equal to it.
    /// Numbers count because plenty of columns hold an id without Lumina knowing what it points
    /// at, and those are exactly the joins nobody has written down.</summary>
    private static bool Points(object? value, uint wanted)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();
        if (type.Name.StartsWith("RowRef", StringComparison.Ordinal))
        {
            try
            {
                return (uint)type.GetProperty("RowId")!.GetValue(value)! == wanted;
            }
            catch (Exception)
            {
                return false;
            }
        }

        return value switch
        {
            uint n => n == wanted,
            int n => n >= 0 && (uint)n == wanted,
            ushort n => n == wanted,
            short n => n >= 0 && (uint)n == wanted,
            _ => false,
        };
    }
}
