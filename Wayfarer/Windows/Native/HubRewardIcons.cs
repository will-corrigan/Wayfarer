using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Windows.Native;

/// <summary>The picture of what an entry gives you, resolved from its
/// <see cref="UnlockReward"/> — a sheet and a row — against the game that is actually installed.
///
/// <para><b>Why the id is not in the catalogue.</b> It would be one lookup cheaper to store the icon
/// beside the reward, and it would be wrong: icon ids are renumbered between patches, and a
/// committed number that has moved draws a band of nothing with no way to notice. The catalogue
/// stores the identity, which does not move, and the id is asked for here. The generator still
/// checks that an icon exists at the moment it writes the field, so a kind that claims to draw
/// something and cannot is caught before it ships — see <c>tools/Wayfarer.CatalogueGen</c>.</para>
///
/// <para><b>The ladder, and where it stops.</b> Twelve kinds carry an icon on their own sheet.
/// <c>Orchestrion</c> has no picture anywhere — the sheet is two columns, Name and Description — so
/// it goes through the roll you are handed. The rest have none at all: there is no artwork for a
/// title, an Aether Current, a folklore book, a crafting-log division or a hunt board. Those return
/// 0, and 0 is an answer. The pane draws the tray and says the reward in words, which is what it
/// would have had to do anyway: KamiToolKit registers tooltips on mouse events only, so an icon is
/// never allowed to be the only place a reward's name appears.</para>
///
/// <para>Resolved once per reward per session. The <b>miss</b> is what has to be cached: an entry
/// with no icon is looked at as often as one with, and a sheet walk per hover is a sheet walk per
/// d-pad step.</para></summary>
internal sealed class HubRewardIcons(IDataManager data, IPluginLog log)
{
    private readonly Dictionary<(string Kind, uint Id), uint> resolved = [];

    private Dictionary<uint, uint>? orchestrionRolls;

    /// <summary>The size the art is authored at, which is what an image node's part rectangle has to
    /// be or it samples past the edge of the texture and draws a band of nothing.
    ///
    /// <para>By KIND, not by id, and that distinction was measured rather than assumed: the sheets'
    /// icon blocks interleave, so no range test can tell them apart. Ornament art runs 786..8057,
    /// straight through the mount block at 4001..4361; BeastTribe's crests at 65016..65131 sit
    /// inside the item range. Every row of every sheet below was read out of the live install and
    /// each one is uniform — 353 mount icons all 40x40, 20 allied-society crests all 36x36, 2,685
    /// hairstyle icons all 96x96.</para>
    ///
    /// <para>This is the seed, not the answer: the caller asks the loaded texture first and only
    /// falls back to this when the game cannot answer yet.</para></summary>
    public static Vector2 SourceSize(string kind) => kind switch
    {
        // Duties draw their content type's glyph, and every ContentType icon is 32x32.
        "ContentFinderCondition" => new Vector2(32f, 32f),

        // The allied societies' crests are the one 36x36 block in the set.
        "BeastTribe" => new Vector2(36f, 36f),

        // Hairstyles are the one oversized one.
        "CharaMakeCustomize" => new Vector2(96f, 96f),

        // Everything else — mount, minion, emote, item, ornament, barding, facewear, Grand Company
        // rank insignia, job soul crystal, orchestrion roll — is authored at 40x40.
        _ => new Vector2(40f, 40f),
    };

    /// <summary>The icon to draw, or 0 when this reward has none and the pane should say it in
    /// words alone. Never throws: a sheet lookup must not be the thing that stops a pane from
    /// being built.</summary>
    public uint For(UnlockReward? reward)
    {
        if (reward is null || reward.Id == 0 || !UnlockRewardKinds.DrawsAnIcon(reward.Kind))
        {
            return 0;
        }

        var key = (reward.Kind, reward.Id);
        if (resolved.TryGetValue(key, out var cached))
        {
            return cached;
        }

        uint icon;
        try
        {
            icon = Lookup(reward);
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer hub: reward {reward.Kind}#{reward.Id} could not be resolved to an icon.");
            icon = 0;
        }

        resolved[key] = icon;
        return icon;
    }

    private static uint Unsigned(int icon) => icon > 0 ? (uint)icon : 0u;

    private uint Lookup(UnlockReward reward) => reward.Kind switch
    {
        "Mount" => data.GetExcelSheet<Mount>().GetRowOrDefault(reward.Id)?.Icon ?? 0u,
        "Companion" => data.GetExcelSheet<Companion>().GetRowOrDefault(reward.Id)?.Icon ?? 0u,
        "Emote" => data.GetExcelSheet<Emote>().GetRowOrDefault(reward.Id)?.Icon ?? 0u,

        // The glyph the duty's own content TYPE carries — the marker the Duty Finder puts beside
        // every dungeon, raid and trial. Neither of ContentFinderCondition's own picture columns
        // fits a 36-pixel square: `Image` is the 376x120 banner, and `Icon` turns out to be the
        // levequest card, 136x168 across all six rows that have one.
        "ContentFinderCondition" => Duty(reward.Id),
        "Item" => ItemIcon(reward.Id),
        "Ornament" => data.GetExcelSheet<Ornament>().GetRowOrDefault(reward.Id)?.Icon ?? 0u,
        "BeastTribe" => data.GetExcelSheet<BeastTribe>().GetRowOrDefault(reward.Id)?.Icon ?? 0u,
        "GrandCompanyRank" => GrandCompanyRank(reward.Id),

        // Barding is a set of three pieces. The body piece is the one that reads as the barding.
        "BuddyEquip" => data.GetExcelSheet<BuddyEquip>().GetRowOrDefault(reward.Id)?.IconBody ?? 0u,
        "Glasses" => Unsigned(data.GetExcelSheet<Glasses>().GetRowOrDefault(reward.Id)?.Icon ?? 0),
        "CharaMakeCustomize" => data.GetExcelSheet<CharaMakeCustomize>().GetRowOrDefault(reward.Id)?.Icon ?? 0u,

        // ClassJob's own icon column is one this Lumina build has not named, so it cannot be cited.
        // The soul crystal IS the job, it is a real reference, and its icon is the picture the game
        // draws on the job's own item.
        "ClassJob" => ItemIcon(data.GetExcelSheet<ClassJob>().GetRowOrDefault(reward.Id)?.ItemSoulCrystal.RowId ?? 0),
        "Orchestrion" => ItemIcon(OrchestrionRoll(reward.Id)),
        _ => 0u,
    };

    private uint ItemIcon(uint rowId) =>
        rowId == 0 ? 0u : data.GetExcelSheet<Item>().GetRowOrDefault(rowId)?.Icon ?? 0u;

    private uint Duty(uint rowId) =>
        data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(rowId)?.ContentType.ValueNullable?.Icon ?? 0u;

    /// <summary>The rank's insignia. Three columns, one per Grand Company, and the Maelstrom's is
    /// taken: the three sets are the same nineteen rank shapes in three liveries, and the pane is
    /// answering "what is this unlock" rather than "what will yours look like". Reading the
    /// player's own company would mean this class knowing about the player, which is a dependency
    /// a name-and-picture lookup should not have.</summary>
    private uint GrandCompanyRank(uint rowId) =>
        Unsigned(data.GetExcelSheet<Lumina.Excel.Sheets.GrandCompanyRank>().GetRowOrDefault(rowId)?.IconMaelstrom ?? 0);

    /// <summary>The Item row that grants an orchestrion roll.
    ///
    /// <para>Built by walking the Item sheet once, on the first orchestrion reward anyone looks at,
    /// and never otherwise: there is no column from an Orchestrion row back to its roll, so the
    /// only way across is to read every item's <c>ItemAction</c> and keep the ones whose type is
    /// 25183. That is fifty thousand rows, which is why it is lazy — and it is one walk, which is
    /// why it is cached rather than avoided.</para></summary>
    private uint OrchestrionRoll(uint orchestrionRowId)
    {
        const uint ItemActionOrchestrion = 25183;

        if (orchestrionRolls is null)
        {
            orchestrionRolls = [];
            foreach (var item in data.GetExcelSheet<Item>())
            {
                if (item.ItemAction.ValueNullable is { } action
                    && action.Action.RowId == ItemActionOrchestrion
                    && item.AdditionalData.RowId != 0)
                {
                    orchestrionRolls.TryAdd(item.AdditionalData.RowId, item.RowId);
                }
            }

            log.Debug($"Wayfarer hub: indexed {orchestrionRolls.Count} orchestrion rolls for their icons.");
        }

        return orchestrionRolls.GetValueOrDefault(orchestrionRowId);
    }
}
