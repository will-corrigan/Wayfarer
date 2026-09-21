using System.Collections;
using System.Reflection;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Wayfarer.SheetReader;

/// <summary>Where every item in the game is spoken of, built once by reading every sheet.
///
/// <para>Answering "where does this come from" by naming the sheet to look in is guesswork, and a
/// wrong guess is indistinguishable from the answer not existing. Naming the sheets that hand
/// things out is the same guess wearing a hat: the list is never complete, and what it misses is
/// silently counted as unknown.</para>
///
/// <para>So nothing is named. Every column of every sheet that points at an item is followed, and
/// what comes back is the complete set of places the game mentions that item. A thing with no
/// mention anywhere is then genuinely absent from the game's data rather than absent from the list
/// of places somebody thought to look.</para></summary>
internal static class Whence
{
    /// <summary>Sheets that say what a thing is rather than where it comes from. A reference from
    /// one of these is not a source, and counting it as one would make everything look accounted
    /// for.</summary>
    private static readonly HashSet<string> NotSources =
        new(StringComparer.Ordinal) { "Item", "Cabinet", "ItemAction", "Transformation", "Recipe", "CompanyCraftPart" };

    /// <summary>Every item, and the rows anywhere that point at it.</summary>
    public static Dictionary<uint, List<string>> Index(GameData game, out int read, out int skipped)
    {
        var found = new Dictionary<uint, List<string>>();
        (read, skipped) = (0, 0);

        foreach (var type in Sheets())
        {
            IEnumerable rows;
            try
            {
                rows = Rows(game, type);
            }
            catch (Exception)
            {
                skipped++;
                continue;
            }

            // Only the columns that are declared to point at an item. A column holding a bare
            // number that happens to equal an item's id is a coincidence, and a scan that counts
            // those reports a balloon and a map marker as places to buy a mount.
            var columns = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => PointsAtItem(p.PropertyType))
                .ToList();
            if (columns.Count == 0)
            {
                continue;
            }

            var id = type.GetProperty("RowId");
            if (id is null)
            {
                continue;
            }

            read++;
            try
            {
                foreach (var row in rows)
                {
                    foreach (var column in columns)
                    {
                        foreach (var item in Items(column, row))
                        {
                            if (!found.TryGetValue(item, out var where))
                            {
                                found[item] = where = [];
                            }

                            where.Add($"{type.Name}[{id.GetValue(row)}].{column.Name}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                skipped++;
            }
        }

        return found;
    }

    /// <summary>Whether a reference is one that says where something came from.</summary>
    public static bool IsSource(string where) =>
        !NotSources.Contains(where[..where.IndexOf('[', StringComparison.Ordinal)]);

    public static IEnumerable<Type> Sheets() => typeof(Addon).Assembly
        .GetTypes()
        .Where(t => t.Namespace == "Lumina.Excel.Sheets" && t.IsValueType && !t.IsEnum && !t.IsNested)
        .OrderBy(t => t.Name, StringComparer.Ordinal);

    public static IEnumerable Rows(GameData game, Type type)
    {
        var subrow = type.GetInterfaces().Any(i => i.Name.StartsWith("IExcelSubrow", StringComparison.Ordinal));
        var get = typeof(ExcelModule).GetMethods()
            .First(m => m.Name == (subrow ? "GetSubrowSheet" : "GetSheet") && m.GetParameters().Length == 2)
            .MakeGenericMethod(type);
        var sheet = get.Invoke(game.Excel, [null, null])!;

        return subrow
            ? ((IEnumerable)sheet).Cast<IEnumerable>().SelectMany(group => group.Cast<object>())
            : (IEnumerable)sheet;
    }

    /// <summary>Whether a column's type is a reference to an item, or a list of them.</summary>
    private static bool PointsAtItem(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("RowRef", StringComparison.Ordinal))
        {
            return type.GetGenericArguments()[0] == typeof(Item);
        }

        return type.IsGenericType
            && type.GetGenericTypeDefinition().Name.StartsWith("Collection", StringComparison.Ordinal)
            && PointsAtItem(type.GetGenericArguments()[0]);
    }

    private static IEnumerable<uint> Items(PropertyInfo column, object row)
    {
        object? value;
        try
        {
            value = column.GetValue(row);
        }
        catch (Exception)
        {
            yield break;
        }

        if (Referenced(value) is { } one)
        {
            if (one != 0)
            {
                yield return one;
            }

            yield break;
        }

        if (value is IEnumerable list)
        {
            foreach (var each in list)
            {
                if (Referenced(each) is { } id && id != 0)
                {
                    yield return id;
                }
            }
        }
    }

    private static uint? Referenced(object? value)
    {
        if (value is null || !value.GetType().Name.StartsWith("RowRef", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return (uint)value.GetType().GetProperty("RowId")!.GetValue(value)!;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
