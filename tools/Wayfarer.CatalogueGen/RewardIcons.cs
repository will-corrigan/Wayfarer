// Does the game actually have a picture of this reward?
//
// The generator asks; the plugin does not read the answer. An icon id is a fact about the patch
// that happens to be installed, so the shipping code looks it up live against its own sheets. What
// this is for is the fence: a kind the catalogue claims draws an icon whose row turns out to have
// none is a DATA bug, and it is caught at generation — where the sheet walk that produced it can
// be corrected — instead of shipping as a blank square only somebody looking at a screen can spot.
//
// The ladder here is the same one Wayfarer/Windows/Native/HubRewardIcons.cs walks at runtime, and
// deliberately so: if the two disagree, the fence is guarding the wrong thing.
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Wayfarer.CatalogueGen;

internal sealed class RewardIcons
{
    private readonly ExcelSheet<Mount> mounts;
    private readonly ExcelSheet<Companion> companions;
    private readonly ExcelSheet<Emote> emotes;
    private readonly ExcelSheet<ContentFinderCondition> duties;
    private readonly ExcelSheet<Item> items;
    private readonly ExcelSheet<Ornament> ornaments;
    private readonly ExcelSheet<BeastTribe> tribes;
    private readonly ExcelSheet<GrandCompanyRank> gcRanks;
    private readonly ExcelSheet<BuddyEquip> bardings;
    private readonly ExcelSheet<Glasses> glasses;
    private readonly ExcelSheet<CharaMakeCustomize> hairstyles;
    private readonly ExcelSheet<ClassJob> jobs;

    private RewardIcons(GameData game)
    {
        mounts = game.GetExcelSheet<Mount>();
        companions = game.GetExcelSheet<Companion>();
        emotes = game.GetExcelSheet<Emote>();
        duties = game.GetExcelSheet<ContentFinderCondition>();
        items = game.GetExcelSheet<Item>();
        ornaments = game.GetExcelSheet<Ornament>();
        tribes = game.GetExcelSheet<BeastTribe>();
        gcRanks = game.GetExcelSheet<GrandCompanyRank>();
        bardings = game.GetExcelSheet<BuddyEquip>();
        glasses = game.GetExcelSheet<Glasses>();
        hairstyles = game.GetExcelSheet<CharaMakeCustomize>();
        jobs = game.GetExcelSheet<ClassJob>();
    }

    public static RewardIcons Build(GameData game) => new(game);

    /// <summary>The icon id, or 0 when this kind has none to give.</summary>
    public uint For(RewardIndex.Candidate candidate) => candidate.Kind switch
    {
        "Mount" => mounts.GetRowOrDefault(candidate.Id)?.Icon ?? 0u,
        "Companion" => companions.GetRowOrDefault(candidate.Id)?.Icon ?? 0u,
        "Emote" => emotes.GetRowOrDefault(candidate.Id)?.Icon ?? 0u,

        // The glyph the duty's own content TYPE carries. Neither of ContentFinderCondition's own
        // picture columns fits a reward square: `Image` is the 376x120 banner and `Icon` is the
        // levequest card, 136x168.
        "ContentFinderCondition" => Duty(candidate.Id),
        "Item" => items.GetRowOrDefault(candidate.Id)?.Icon ?? 0u,
        "Ornament" => ornaments.GetRowOrDefault(candidate.Id)?.Icon ?? 0u,
        "BeastTribe" => tribes.GetRowOrDefault(candidate.Id)?.Icon ?? 0u,

        // Three columns, one per Grand Company. Which one to draw is a property of the player, not
        // of the reward, so the generator only asks whether the row has art at all.
        "GrandCompanyRank" => Unsigned(gcRanks.GetRowOrDefault(candidate.Id)?.IconMaelstrom ?? 0),

        // Barding is a set of three. The body piece is the one that reads as the barding.
        "BuddyEquip" => bardings.GetRowOrDefault(candidate.Id)?.IconBody ?? 0u,
        "Glasses" => Unsigned(glasses.GetRowOrDefault(candidate.Id)?.Icon ?? 0),
        "CharaMakeCustomize" => hairstyles.GetRowOrDefault(candidate.Id)?.Icon ?? 0u,

        // ClassJob's own icon column is one of the columns this Lumina build has not named, so it
        // cannot be cited. The soul crystal is the job, it is a real reference, and its icon is the
        // picture the game itself uses on the job's own item.
        "ClassJob" => items.GetRowOrDefault(jobs.GetRowOrDefault(candidate.Id)?.ItemSoulCrystal.RowId ?? 0)?.Icon ?? 0u,

        // The Orchestrion sheet is Name and Description; the roll you are handed is the only thing
        // with a picture.
        "Orchestrion" => items.GetRowOrDefault(candidate.GrantingItemId)?.Icon ?? 0u,
        _ => 0u,
    };

    private static uint Unsigned(int icon) => icon > 0 ? (uint)icon : 0u;

    private uint Duty(uint rowId) => duties.GetRowOrDefault(rowId)?.ContentType.ValueNullable?.Icon ?? 0u;
}
