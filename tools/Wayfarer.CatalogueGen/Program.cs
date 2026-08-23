// Resolves wiki link targets to game row ids, for scripts/build-unlock-catalogue.mjs.
//
// The generator is split at exactly this line for one reason: GitHub's CI runners have no game
// installation, so nothing that needs sqpack may ever be on the validation path. Generation is a
// local, developer-only step that produces data/unlocks-by-level.json; that file is committed and
// CI validates the committed file with no game data at all.
//
// This half owns every decision that has to agree with the running plugin — folding a name into a
// match key, and choosing between Quest rows that share one — because those are Wayfarer.Core's
// QuestNameKey and QuestNameMatch, referenced here rather than reimplemented. The .mjs half never
// normalises a name; it sends raw link targets over and reads row ids back.
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.CatalogueGen;

internal static class Program
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static int Main(string[] args)
    {
        if (args.Length != 3 || !string.Equals(args[0], "resolve", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("usage: Wayfarer.CatalogueGen resolve <request.json> <response.json>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  request.json  { \"sqpack\": \"<path>\", \"names\": [ ... ] }");
            Console.Error.WriteLine("  response.json { \"names\": { \"<raw name>\": { \"key\": ..., \"quest\": ... } } }");
            return 2;
        }

        var request = JsonSerializer.Deserialize<ResolveRequest>(File.ReadAllText(args[1]), ReadOptions)
            ?? throw new InvalidOperationException($"could not read a request from {args[1]}");

        var sqpack = request.Sqpack;
        if (string.IsNullOrWhiteSpace(sqpack) || !Directory.Exists(sqpack))
        {
            Console.Error.WriteLine($"sqpack directory not found: '{sqpack}'");
            Console.Error.WriteLine("Generation needs a local game installation. CI validates the committed dataset instead.");
            return 3;
        }

        var game = new GameData(sqpack);
        var index = SheetIndex.Build(game);
        Console.Error.WriteLine(
            $"indexed {index.QuestRows.Count} quest rows, {index.Sheets["duty"].Count} duty keys, {index.Sheets["item"].Count} item keys");

        var names = new SortedDictionary<string, ResolvedName>(StringComparer.Ordinal);
        foreach (var raw in request.Names.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
        {
            names[raw] = index.Resolve(raw);
        }

        var response = new ResolveResponse
        {
            Sqpack = sqpack,
            QuestRowCount = index.QuestRows.Count,
            Names = names,
            Quests = index.FactsFor(names.Values, request.QuestRowIds),
        };

        File.WriteAllText(args[2], JsonSerializer.Serialize(response, WriteOptions));
        Console.Error.WriteLine($"resolved {names.Count} names -> {args[2]}");
        return 0;
    }
}

internal sealed class ResolveRequest
{
    public string Sqpack { get; init; } = string.Empty;

    public List<string> Names { get; init; } = [];

    /// <summary>Quest rows to return facts for even though no name resolved to them. The caller
    /// gets some row ids from a wiki page's own infobox rather than from a name, and still needs
    /// the row's level and display name.</summary>
    public List<uint> QuestRowIds { get; init; } = [];
}

internal sealed class ResolveResponse
{
    public string Sqpack { get; init; } = string.Empty;

    public int QuestRowCount { get; init; }

    /// <summary>Keyed by the raw link target exactly as it appeared in the wikitext, so the
    /// caller never has to reproduce the folding to look an answer up.</summary>
    public SortedDictionary<string, ResolvedName> Names { get; init; } = [];

    /// <summary>Facts for every quest row any resolution names, so the caller can emit a level
    /// or a display name without a second round trip.</summary>
    public SortedDictionary<string, QuestFacts> Quests { get; init; } = [];
}

internal sealed class ResolvedName
{
    public string Key { get; init; } = string.Empty;

    /// <summary>The row <see cref="QuestNameMatch"/> binds, or null when no Quest row carries
    /// this key.</summary>
    public uint? QuestRowId { get; init; }

    /// <summary>Every row that ties with the bound one on the documented tie-break, empty when
    /// the choice was unambiguous. A non-empty list means "any of these", never "pick one":
    /// the three <c>Simply the Hest</c> rows are one per starting city and a character holds
    /// exactly one.</summary>
    public List<uint> QuestAnyOf { get; init; } = [];

    /// <summary>Every Quest row sharing the key, including the ones the tie-break rejected.
    /// Kept so a regeneration diff can show that a retired duplicate was passed over rather
    /// than never seen.</summary>
    public List<uint> QuestCandidates { get; init; } = [];

    /// <summary>Rows whose name is this name plus a trailing parenthetical — "Squadron and
    /// Commander (Maelstrom)" for "Squadron and Commander". Populated ONLY when the name itself
    /// matches no Quest row, and never used to pick one.
    ///
    /// <para>This is deliberately not part of <see cref="QuestNameKey"/> and must never become
    /// so. Folding the parenthetical away as a matching rule is what the name-reconciliation
    /// audit measured and rejected: it collapses the ten "A Relic Reborn" weapon rows and every
    /// Grand Company triple onto one key, so a name match would then bind an arbitrary one. What
    /// this field supports is the opposite operation — recording the whole SET as alternatives,
    /// which is what the guide means by "one of the A Relic Reborn Sidequests" and "the
    /// applicable Let the Hunt Begin sidequest". Picking is the error; enumerating is the
    /// fix.</para></summary>
    public List<uint> QuestVariants { get; init; } = [];

    public SortedDictionary<string, List<SheetHit>> Sheets { get; init; } = [];
}

internal sealed class SheetHit
{
    public uint RowId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>ContentFinderCondition.ClassJobLevelRequired, for the duty-page infobox
    /// cross-check. Null on sheets that have no such column.</summary>
    public int? Level { get; init; }

    /// <summary>For duty rows only: the <c>InstanceContent</c> row that points at this
    /// ContentFinderCondition.
    ///
    /// <para>Two ids for one duty is not redundancy. The wiki and the guide name duties the way
    /// ContentFinderCondition does, so that is the row a link target resolves to and the row the
    /// catalogue cites as provenance — but <c>UIState.IsInstanceContentCompleted</c> takes an
    /// InstanceContent id, and the two numbering schemes are unrelated (Basic Training: Enemy
    /// Parties is CFC 42 and InstanceContent 10001). Emitting both here is what lets the
    /// catalogue cite the row a human can look up and still hand the plugin the row it can
    /// actually ask about.</para></summary>
    public uint? ContentId { get; init; }
}

internal sealed class QuestFacts
{
    public uint RowId { get; init; }

    /// <summary>The name with the journal icon glyph folded off — what a player would read.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary><c>ClassJobLevel[0] + QuestLevelOffset</c>. The sheet splits the accept level
    /// across two columns and the offset one is not cosmetic: it is the difference between
    /// level 71 and level 80 for The Bozjan Southern Front.</summary>
    public int Level { get; init; }

    public uint JournalGenre { get; init; }

    public string PlaceName { get; init; } = string.Empty;

    public int InboundPrereqReferences { get; init; }
}

/// <summary>Every sheet the guide's link targets can land in, folded onto match keys once.</summary>
internal sealed class SheetIndex
{
    private SheetIndex(
        Dictionary<string, List<QuestNameCandidate>> questsByKey,
        Dictionary<string, List<uint>> questVariantsByBaseKey,
        Dictionary<uint, QuestFacts> questRows,
        Dictionary<string, Dictionary<string, List<SheetHit>>> sheets)
    {
        this.QuestsByKey = questsByKey;
        this.QuestVariantsByBaseKey = questVariantsByBaseKey;
        this.QuestRows = questRows;
        this.Sheets = sheets;
    }

    public Dictionary<string, List<QuestNameCandidate>> QuestsByKey { get; }

    public Dictionary<string, List<uint>> QuestVariantsByBaseKey { get; }

    public Dictionary<uint, QuestFacts> QuestRows { get; }

    public Dictionary<string, Dictionary<string, List<SheetHit>>> Sheets { get; }

    public static SheetIndex Build(GameData game)
    {
        var quests = game.GetExcelSheet<Quest>() ?? throw new InvalidOperationException("no Quest sheet");

        var inbound = new Dictionary<uint, int>();
        foreach (var q in quests)
        {
            foreach (var prev in q.PreviousQuest)
            {
                if (prev.RowId != 0)
                {
                    inbound[prev.RowId] = inbound.GetValueOrDefault(prev.RowId) + 1;
                }
            }
        }

        var questsByKey = new Dictionary<string, List<QuestNameCandidate>>(StringComparer.Ordinal);
        var variantsByBaseKey = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
        var questRows = new Dictionary<uint, QuestFacts>();
        foreach (var q in quests)
        {
            var raw = q.Name.ExtractText();
            if (raw.Length == 0)
            {
                continue;
            }

            // "Squadron and Commander (Maelstrom)" also files under "squadron and commander", in
            // a separate index that is only ever read as a SET. See ResolvedName.QuestVariants.
            var display = QuestNameKey.Display(raw);
            var open = display.LastIndexOf('(');
            if (open > 0 && display.EndsWith(')') && display.IndexOf(')', open) == display.Length - 1)
            {
                var baseKey = QuestNameKey.For(display[..open].TrimEnd());
                if (baseKey.Length > 0)
                {
                    if (!variantsByBaseKey.TryGetValue(baseKey, out var variants))
                    {
                        variantsByBaseKey[baseKey] = variants = [];
                    }

                    variants.Add(q.RowId);
                }
            }

            var refs = inbound.GetValueOrDefault(q.RowId);
            questRows[q.RowId] = new QuestFacts
            {
                RowId = q.RowId,
                Name = QuestNameKey.Display(raw),
                Level = q.ClassJobLevel[0] + q.QuestLevelOffset,
                JournalGenre = q.JournalGenre.RowId,
                PlaceName = q.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty,
                InboundPrereqReferences = refs,
            };

            var key = QuestNameKey.For(raw);
            if (!questsByKey.TryGetValue(key, out var list))
            {
                questsByKey[key] = list = [];
            }

            list.Add(new QuestNameCandidate(q.RowId, q.JournalGenre.RowId, refs));
        }

        var sheets = new Dictionary<string, Dictionary<string, List<SheetHit>>>(StringComparer.Ordinal)
        {
            ["duty"] = Fold(
                game.GetExcelSheet<ContentFinderCondition>(),
                r => r.Name.ExtractText(),
                r => r.ClassJobLevelRequired,
                InstanceContentOf),
            ["item"] = Fold(game.GetExcelSheet<Item>(), r => r.Name.ExtractText()),
            ["mount"] = Fold(game.GetExcelSheet<Mount>(), r => r.Singular.ExtractText()),
            ["minion"] = Fold(game.GetExcelSheet<Companion>(), r => r.Singular.ExtractText()),
            ["emote"] = Fold(game.GetExcelSheet<Emote>(), r => r.Name.ExtractText()),
            ["orchestrion"] = Fold(game.GetExcelSheet<Orchestrion>(), r => r.Name.ExtractText()),
            ["card"] = Fold(game.GetExcelSheet<TripleTriadCard>(), r => r.Name.ExtractText()),
            ["action"] = Fold(game.GetExcelSheet<Lumina.Excel.Sheets.Action>(), r => r.Name.ExtractText()),
        };

        return new SheetIndex(questsByKey, variantsByBaseKey, questRows, sheets);
    }

    public ResolvedName Resolve(string raw)
    {
        var key = QuestNameKey.For(raw);
        var resolved = new ResolvedName { Key = key };

        if (this.QuestsByKey.TryGetValue(key, out var candidates) && candidates.Count > 0)
        {
            var match = QuestNameMatch.Resolve(candidates);
            resolved = new ResolvedName
            {
                Key = key,
                QuestRowId = match.Best.RowId,
                QuestAnyOf = [.. match.Alternatives.Order()],
                QuestCandidates = [.. candidates.Select(c => c.RowId).Order()],
            };
        }
        else if (this.QuestVariantsByBaseKey.TryGetValue(key, out var variants) && variants.Count > 1)
        {
            // No row carries this name, but several carry it with a parenthetical after it. The
            // caller decides whether the guide's sentence means "any of these"; all this does is
            // hand over the set, never one of them.
            resolved = new ResolvedName { Key = key, QuestVariants = [.. variants.Order()] };
        }

        foreach (var (sheet, byKey) in this.Sheets)
        {
            if (byKey.TryGetValue(key, out var hits) && hits.Count > 0)
            {
                resolved.Sheets[sheet] = hits;
            }
        }

        return resolved;
    }

    public SortedDictionary<string, QuestFacts> FactsFor(IEnumerable<ResolvedName> names, IEnumerable<uint> alsoWanted)
    {
        var wanted = new SortedSet<uint>(alsoWanted);
        foreach (var n in names)
        {
            foreach (var id in n.QuestCandidates)
            {
                wanted.Add(id);
            }

            foreach (var id in n.QuestVariants)
            {
                wanted.Add(id);
            }
        }

        var facts = new SortedDictionary<string, QuestFacts>(StringComparer.Ordinal);
        foreach (var id in wanted)
        {
            if (this.QuestRows.TryGetValue(id, out var f))
            {
                facts[id.ToString(CultureInfo.InvariantCulture)] = f;
            }
        }

        return facts;
    }

    /// <summary>The InstanceContent row a duty row points at — the id
    /// <c>UIState.IsInstanceContentCompleted</c> takes.
    ///
    /// <para>Read from the ContentFinderCondition row's own <c>Content</c> reference rather than
    /// by scanning InstanceContent for rows that point back here, because that scan is ambiguous
    /// and demonstrably wrong once: two InstanceContent rows name ContentFinderCondition 16, and
    /// the Praetorium's live one is 86, not the retired 16 that a first-match scan returns.
    /// <c>ContentLinkType</c> is what says the reference is an InstanceContent row at all, so it
    /// is checked rather than assumed.</para></summary>
    private static uint? InstanceContentOf(ContentFinderCondition row) =>
        row.ContentLinkType == 1 && row.Content.RowId != 0 ? row.Content.RowId : null;

    private static Dictionary<string, List<SheetHit>> Fold<T>(
        Lumina.Excel.ExcelSheet<T>? sheet,
        Func<T, string> name,
        Func<T, int>? level = null,
        Func<T, uint?>? contentId = null)
        where T : struct, Lumina.Excel.IExcelRow<T>
    {
        var byKey = new Dictionary<string, List<SheetHit>>(StringComparer.Ordinal);
        if (sheet is null)
        {
            return byKey;
        }

        foreach (var row in sheet)
        {
            var raw = name(row);
            if (raw.Length == 0)
            {
                continue;
            }

            var key = QuestNameKey.For(raw);
            if (!byKey.TryGetValue(key, out var list))
            {
                byKey[key] = list = [];
            }

            list.Add(new SheetHit
            {
                RowId = row.RowId,
                Name = QuestNameKey.Display(raw),
                Level = level?.Invoke(row),
                ContentId = contentId?.Invoke(row),
            });
        }

        return byKey;
    }
}
