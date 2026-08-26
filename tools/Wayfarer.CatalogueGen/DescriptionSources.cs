// Where the GAME says what a thing is, for the entries nobody has written a sentence about.
//
// WHY A REFERENCE AND NOT THE TEXT
// The catalogue imports 621 entries from the game's own sheets, and the sheets state a name and a
// gate and no prose. Writing a description for each would mean generating 621 sentences, and a
// manufactured sentence that reads like curation is the same error as an invented level in the field
// a player reads first. But for several hundred of them the game HAS a sentence — Square Enix's own,
// already localised into whatever language the player's client runs in — and the repository already
// has the mechanism for citing one rather than copying it: GameTextRef, a sheet name, a row and a
// column, resolved live by UnlockGateContext.ResolveGameText. See Wayfarer.Core/Unlocks/GameTextRef.
//
// So this finds the reference. It never reads the text into the dataset.
//
// HOW THE COLUMN INDEX IS FOUND
// Not by counting columns in a schema definition, which is exactly the kind of number that moves
// between patches and fails silently. The typed sheet already knows how to read the field, and the
// RawRow API reads columns by index, so the index is DERIVED: read the field the typed way, then
// find the raw column that produces the same string, and require several rows to agree on the same
// index before believing it. A sheet whose column cannot be pinned down that way gets no reference
// at all, which costs a description and cannot produce a wrong one.
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Wayfarer.CatalogueGen;

/// <summary>A resolved pointer at one string cell.</summary>
internal readonly record struct DescriptionSource(string Sheet, uint Row, int Column);

internal sealed class DescriptionSources
{
    /// <summary>How many rows have to agree on a column index before it is used. One row agreeing is
    /// a coincidence waiting to happen — plenty of sheets carry the same short string in two columns
    /// — and the whole point of deriving the index is not to guess it.</summary>
    private const int AgreementsRequired = 8;

    private readonly GameData game;
    private readonly Dictionary<string, int?> columns = new(StringComparer.Ordinal);

    private DescriptionSources(GameData game) => this.game = game;

    public static DescriptionSources Build(GameData game) => new(game);

    /// <summary>The description of an achievement, which is where a quest TITLE's own sentence
    /// lives: the Title sheet has nothing but the three grammatical forms of the title itself, and
    /// the achievement that grants it is the row that says what earning it meant.</summary>
    public DescriptionSource? ForAchievement(uint achievementRowId) =>
        For<Achievement>("Achievement", achievementRowId, r => r.Description.ExtractText());

    /// <summary>An orchestrion roll's own description. The sheet is two columns, Name and
    /// Description, and the second is the only prose the game has about a roll.</summary>
    public DescriptionSource? ForOrchestrion(uint orchestrionRowId) =>
        For<Orchestrion>("Orchestrion", orchestrionRowId, r => r.Description.ExtractText());

    /// <summary>What the game says about the identity itself, for the channels whose own sheet — or
    /// the transient sheet beside it — carries a sentence. One line per channel, so a channel that
    /// gets no description is a channel nobody has looked for one on rather than a silent gap.
    ///
    /// <para>Returns null both when the sheet has no such column and when this row's cell is empty.
    /// The two are different facts about the game and neither is a defect in the catalogue.</para>
    /// </summary>
    public DescriptionSource? ForIdentity(string kind, uint rowId) => kind switch
    {
        "Mount" => For<MountTransient>("MountTransient", rowId, r => r.Description.ExtractText()),
        "Companion" => For<CompanionTransient>("CompanionTransient", rowId, r => r.Description.ExtractText()),
        "ContentsNote" => For<ContentsNote>("ContentsNote", rowId, r => r.Description.ExtractText()),
        "GeneralAction" => For<GeneralAction>("GeneralAction", rowId, r => r.Description.ExtractText()),

        // A framer's kit IS its item, which is the one channel where the identity's own sheet has the
        // prose rather than a transient beside it.
        "Item" => For<Item>("Item", rowId, r => r.Description.ExtractText()),
        _ => null,
    };

    /// <summary>A duty's own Duty Finder blurb. It lives on the transient sheet beside the duty
    /// rather than on the duty row, and it is the text the client itself prints under a duty's
    /// name.</summary>
    public DescriptionSource? ForDuty(uint contentFinderConditionRowId) =>
        For<ContentFinderConditionTransient>(
            "ContentFinderConditionTransient",
            contentFinderConditionRowId,
            r => r.Description.ExtractText());

    /// <summary>The reference, or null when this sheet's column could not be pinned down or this
    /// row's cell is empty. Both are ordinary answers: a row with no description simply has
    /// none.</summary>
    private DescriptionSource? For<T>(string sheetName, uint rowId, Func<T, string> read)
        where T : struct, IExcelRow<T>
    {
        if (Column(sheetName, read) is not { } column)
        {
            return null;
        }

        var sheet = game.GetExcelSheet<T>();
        if (sheet.GetRowOrDefault(rowId) is not { } row || read(row).Length == 0)
        {
            return null;
        }

        // Verified on the row that will actually be cited, not only on the sample that established
        // the index. A reference the generator has not read successfully is not written.
        return RawText(sheetName, rowId, column) == read(row)
            ? new DescriptionSource(sheetName, rowId, column)
            : null;
    }

    /// <summary>The raw column index this sheet's description lives in, derived once.</summary>
    private int? Column<T>(string sheetName, Func<T, string> read)
        where T : struct, IExcelRow<T>
    {
        if (columns.TryGetValue(sheetName, out var cached))
        {
            return cached;
        }

        var found = Derive(sheetName, read);
        columns[sheetName] = found;
        Console.Error.WriteLine(found is { } c
            ? $"description column for {sheetName}: {c.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : $"description column for {sheetName}: NOT FOUND — those entries ship with no description reference");
        return found;
    }

    private int? Derive<T>(string sheetName, Func<T, string> read)
        where T : struct, IExcelRow<T>
    {
        var typed = game.GetExcelSheet<T>();
        var raw = game.Excel.GetSheet<RawRow>(null, sheetName);
        if (typed is null || raw is null)
        {
            return null;
        }

        var agreements = new Dictionary<int, int>();
        var sampled = 0;
        foreach (var row in typed)
        {
            var wanted = read(row);
            if (wanted.Length < 12)
            {
                // Short strings collide across columns. A description is a sentence; sampling one
                // that is not gives the coincidence a chance it does not need.
                continue;
            }

            if (!raw.TryGetRow(row.RowId, out var rawRow))
            {
                continue;
            }

            for (var column = 0; column < 64; column++)
            {
                string candidate;
                try
                {
                    candidate = rawRow.ReadStringColumn(column).ExtractText();
                }
                catch (Exception)
                {
                    // A column past the end of the sheet, or one this Lumina build models as something
                    // other than a string. Both mean "not this one" — and it has to CONTINUE rather
                    // than stop: Achievement's column 0 throws and its description is at 8, so
                    // stopping at the first refusal found nothing at all.
                    continue;
                }

                if (string.Equals(candidate, wanted, StringComparison.Ordinal))
                {
                    agreements[column] = agreements.GetValueOrDefault(column) + 1;
                }
            }

            if (++sampled >= AgreementsRequired * 2)
            {
                break;
            }
        }

        // Exactly one column that every sampled row agreed on. Two would mean the sheet duplicates
        // its description and the choice would be arbitrary; none means the typed accessor is not
        // reading a plain string column and the raw API cannot reproduce it.
        var unanimous = agreements.Where(kv => kv.Value >= Math.Min(AgreementsRequired, sampled)).ToList();
        return unanimous.Count == 1 ? unanimous[0].Key : null;
    }

    private string RawText(string sheetName, uint rowId, int column)
    {
        var raw = game.Excel.GetSheet<RawRow>(null, sheetName);
        return raw is not null && raw.TryGetRow(rowId, out var row)
            ? row.ReadStringColumn(column).ExtractText()
            : string.Empty;
    }
}
