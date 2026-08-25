using Dalamud.Plugin.Services;
using Lumina.Excel;

namespace Wayfarer;

/// <summary>Reads one string cell out of the running client's own sheets, by sheet name, row and
/// column rather than a strongly-typed wrapper — the escape hatch Lumina offers for sheets it has
/// no typed row for.
///
/// <para><b>Why this is one method and not two.</b> <see cref="UnlockService"/>'s resolution of a
/// <c>GameTextRef</c> and <see cref="Windows.Native.JournalWords"/>'s resolution of the journal
/// window's own section headings were the same five lines — <c>GetSheet&lt;RawRow&gt;</c>,
/// <c>TryGetRow</c>, <c>ReadStringColumn(...).ExtractText()</c>, null on blank — written twice a day
/// apart. Both callers keep their own caching and their own fallback text, because those differ; the
/// sheet read itself does not.</para></summary>
internal static class GameSheetText
{
    /// <summary>The cell's text, or null when the sheet or row could not be found, or the cell was
    /// blank. Throws on whatever <c>GetSheet</c>/<c>TryGetRow</c> throw — callers decide how a
    /// resolution failure is logged, since one of them logs once ever and the other logs once per
    /// reference.</summary>
    public static string? Read(IDataManager data, string sheet, uint row, int column)
    {
        var excelSheet = data.Excel.GetSheet<RawRow>(null, sheet);
        if (!excelSheet.TryGetRow(row, out var value))
        {
            return null;
        }

        var text = value.ReadStringColumn(column).ExtractText();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
