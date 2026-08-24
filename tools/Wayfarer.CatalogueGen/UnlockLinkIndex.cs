// The name lookups RewardIndex needs, and the two sheets that are keyed on something other than
// their own row id.
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Wayfarer.CatalogueGen;

/// <summary>Names for the sheets an <c>ItemAction</c> can point at, plus the UnlockLink table that
/// two of them are addressed through instead of by row id.</summary>
internal sealed class UnlockLinkIndex
{
    private readonly ExcelSheet<Mount> mounts;
    private readonly ExcelSheet<Companion> companions;
    private readonly ExcelSheet<Orchestrion> orchestrion;
    private readonly ExcelSheet<TripleTriadCard> cards;
    private readonly ExcelSheet<BuddyEquip> bardings;
    private readonly ExcelSheet<Ornament> ornaments;
    private readonly ExcelSheet<Glasses> glasses;

    /// <summary>UnlockLink id to the emote or hairstyle it opens. Two sheets, one id space, and no
    /// column anywhere says which of them a given id belongs to — the only way to know is to look
    /// in both. Emotes win a collision because <c>Emote.UnlockLink</c> is the older and denser of
    /// the two; in practice the sets are disjoint.</summary>
    private readonly Dictionary<uint, RewardIndex.Candidate> byUnlockLink;

    private UnlockLinkIndex(GameData game)
    {
        mounts = game.GetExcelSheet<Mount>();
        companions = game.GetExcelSheet<Companion>();
        orchestrion = game.GetExcelSheet<Orchestrion>();
        cards = game.GetExcelSheet<TripleTriadCard>();
        bardings = game.GetExcelSheet<BuddyEquip>();
        ornaments = game.GetExcelSheet<Ornament>();
        glasses = game.GetExcelSheet<Glasses>();

        byUnlockLink = [];
        foreach (var row in game.GetExcelSheet<CharaMakeCustomize>())
        {
            var name = row.HintItem.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (row.UnlockLink != 0 && name.Length > 0)
            {
                byUnlockLink[row.UnlockLink] =
                    new RewardIndex.Candidate("CharaMakeCustomize", row.RowId, name, "CharaMakeCustomize.UnlockLink");
            }
        }

        foreach (var row in game.GetExcelSheet<Emote>())
        {
            var name = row.Name.ExtractText();
            if (row.UnlockLink != 0 && name.Length > 0)
            {
                byUnlockLink[row.UnlockLink] =
                    new RewardIndex.Candidate("Emote", row.RowId, name, "Emote.UnlockLink");
            }
        }
    }

    public static UnlockLinkIndex Build(GameData game) => new(game);

    public string MountName(uint id) => mounts.GetRowOrDefault(id)?.Singular.ExtractText() ?? string.Empty;

    public string CompanionName(uint id) => companions.GetRowOrDefault(id)?.Singular.ExtractText() ?? string.Empty;

    public string OrchestrionName(uint id) => orchestrion.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty;

    public string CardName(uint id) => cards.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty;

    public string BardingName(uint id) => bardings.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty;

    public string OrnamentName(uint id) => ornaments.GetRowOrDefault(id)?.Singular.ExtractText() ?? string.Empty;

    public string GlassesName(uint id) => glasses.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty;

    /// <summary>The emote or hairstyle an UnlockLink id opens, or null when it opens neither — the
    /// Aetheryte Pendulum is the case that proves the null is needed.</summary>
    public RewardIndex.Candidate? FromUnlockLink(uint id) =>
        id == 0 ? null : byUnlockLink.GetValueOrDefault(id);
}

/// <summary>Grand Company rank names. <c>GrandCompanyRank</c> itself has no name column — the words
/// live in six per-company, per-gender text sheets keyed on the same row id. The company is read
/// from the granting quest, because a rank is only ever awarded by one company's promotion quest;
/// the masculine form is taken for both, exactly as the rest of the catalogue's prose is.</summary>
internal sealed class GrandCompanyRankNames
{
    private const uint Maelstrom = 1;
    private const uint TwinAdder = 2;
    private const uint ImmortalFlames = 3;

    private readonly ExcelSheet<GCRankLimsaMaleText> maelstrom;
    private readonly ExcelSheet<GCRankGridaniaMaleText> adder;
    private readonly ExcelSheet<GCRankUldahMaleText> flames;

    private GrandCompanyRankNames(GameData game)
    {
        maelstrom = game.GetExcelSheet<GCRankLimsaMaleText>();
        adder = game.GetExcelSheet<GCRankGridaniaMaleText>();
        flames = game.GetExcelSheet<GCRankUldahMaleText>();
    }

    public static GrandCompanyRankNames Build(GameData game) => new(game);

    public string NameFor(uint rankRowId, uint grandCompanyRowId) => grandCompanyRowId switch
    {
        Maelstrom => maelstrom.GetRowOrDefault(rankRowId)?.NameRank.ExtractText() ?? string.Empty,
        TwinAdder => adder.GetRowOrDefault(rankRowId)?.NameRank.ExtractText() ?? string.Empty,
        ImmortalFlames => flames.GetRowOrDefault(rankRowId)?.NameRank.ExtractText() ?? string.Empty,
        _ => string.Empty,
    };
}
