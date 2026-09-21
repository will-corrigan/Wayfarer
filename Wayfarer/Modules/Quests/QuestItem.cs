using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace Wayfarer.Modules.Quests;

/// <summary>A key item a ToDo has the player use.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Name">The item's name, as the sheet titles it.</param>
/// <param name="Usable">Whether the game gives the item anything to do. One that does not is
/// carried and handed over, and a step naming it is a delivery rather than a use.</param>
/// <param name="Called">Every name the sheet gives it: its title, and how it reads on its own and
/// in numbers. A step writes about an item in whichever of these suits the sentence.</param>
internal sealed record QuestItem(uint Id, string Name, bool Usable, IReadOnlyList<string>? Called = null)
{
    /// <summary>Reads an item out of the sheet, with every name the sheet gives it: its title, and
    /// how it reads on its own and in numbers.</summary>
    /// <param name="id">The item's id.</param>
    /// <param name="row">The sheet row.</param>
    public static QuestItem Of(uint id, EventItem row) => new(
        id,
        row.Name.ExtractText(),
        row.Action.RowId != 0,
        [.. new[] { row.Name.ExtractText(), row.Singular.ExtractText(), row.Plural.ExtractText() }
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)]);

    /// <summary>Whether an id is a key item's rather than an ordinary item's. Key items are what the
    /// game calls event items: they live in their own range and are used through a different action
    /// kind, so a ToDo's ordinary item is a turn-in and not a use. Dalamud knows where that range
    /// is and that an id inside it is really one of them.</summary>
    public static bool IsKeyItem(uint itemId) => ItemUtil.IsEventItem(itemId);

    /// <summary>The words a step used for this item, or null when it named it not at all.
    ///
    /// <para>A step writes an item's name as the sentence needs it, and the sheet's own names do
    /// not always include that: a step may ask for a grenade where the sheet titles them grenades,
    /// or for sacks where the sheet titles one sack. So each name is tried both as it stands and
    /// without a plural ending, and the longest that appears is the one the step meant.</para></summary>
    /// <param name="words">The step's own sentence.</param>
    public string? NamedIn(string words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return (Called ?? [Name])
            .SelectMany(Forms)
            .Where(form => form.Length > 0 && words.Contains(form, StringComparison.OrdinalIgnoreCase))
            .MaxBy(form => form.Length);
    }

    /// <summary>A name as it stands, and as it reads with a plural ending taken off.</summary>
    private static IEnumerable<string> Forms(string called) =>
        called.EndsWith('s') || called.EndsWith('S') ? [called, called[..^1]] : [called];
}
