using System.Globalization;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Windows.Native;

/// <summary>The two things about an entry that only the installed game can answer, and that the
/// journal page needs and the strip did not: the banner it draws, and where on the map its quest
/// giver stands.
///
/// <para><b>The banner.</b> <c>JournalDetail #45</c> is a 376x120 slot and the game authors exactly
/// two kinds of art for it. Measured against the live install rather than trusted: all 2,519
/// non-zero <c>Quest.Icon</c> rows are 376x120 and every file is present, and so are all 773
/// non-zero <c>ContentFinderCondition.Image</c> rows. Nothing else in the icon table is that shape,
/// which is why <see cref="SourceSize"/> is a constant rather than a range test — the lesson the
/// reward icons taught is that the sheets' icon blocks interleave and an id cannot be sized by
/// its number.</para>
///
/// <para><b>The ladder.</b> The duty's banner first, because it shows the place you are being sent;
/// the gate quest's second; nothing third. A placeholder would be worse than a gap: the page is
/// laid out so the block is dropped whole, and 166 of the 587 shipped entries have no art of either
/// kind — mostly the system unlocks, which are features rather than places.</para>
///
/// <para>Resolved once per entry per session, misses included: an entry with no banner is looked at
/// as often as one with, and a sheet walk per hover is a sheet walk per d-pad step.</para></summary>
internal sealed class HubJournalFacts(IDataManager data, ITextureProvider textures, IPluginLog log)
{
    private readonly Dictionary<uint, uint> banners = [];

    private readonly Dictionary<uint, int> dutyLevels = [];

    private bool loggedMissingBanner;

    /// <summary>The size the banner art is authored at, which is what an image node's part rectangle
    /// has to be or it samples past the edge of the texture and draws a band of nothing.
    /// <c>IconImageNode</c>'s own default is 32x32, which would draw the top-left corner of the
    /// picture and nothing else.</summary>
    public static System.Numerics.Vector2 SourceSize =>
        new(Core.Ui.GameMetrics.Journal.BannerWidth, Core.Ui.GameMetrics.Journal.BannerHeight);

    /// <summary>The 376x120 banner for an entry, or 0 when the game ships none for it.</summary>
    public uint Banner(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        // Keyed on the quest row because that is what an entry is identified by everywhere else in
        // the plugin, and 0 is a real key — the entries with no quest all resolve to no banner and
        // want to be remembered as such.
        var key = unlock.QuestRowId ?? 0u;
        if (banners.TryGetValue(key, out var cached))
        {
            return cached;
        }

        uint icon;
        try
        {
            icon = Duty(unlock) is var duty and not 0 ? duty : Quest(unlock);
            icon = Drawable(icon) ? icon : 0u;
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer hub: no banner could be resolved for '{unlock.Def.Unlock}'.");
            icon = 0;
        }

        banners[key] = icon;
        return icon;
    }

    /// <summary>The duty's own sync level — <c>ContentFinderCondition.ClassJobLevelRequired</c> —
    /// so the reward tray can say "Sastasha (Lv. 15)" instead of just the name. This is
    /// deliberately not the entry's own <see cref="ResolvedUnlock.QuestLevel"/>: that is the
    /// unlocking quest's accept level, which the badge already shows and which is not always the
    /// duty's own sync level. 0 when the row cannot be read — <see cref="UnlockRowText.DutyReward"/>
    /// is what turns that into "no level printed" rather than "(Lv. 0)".</summary>
    public int DutyLevel(uint contentFinderConditionRowId)
    {
        if (contentFinderConditionRowId == 0)
        {
            return 0;
        }

        if (dutyLevels.TryGetValue(contentFinderConditionRowId, out var cached))
        {
            return cached;
        }

        var level = 0;
        try
        {
            level = data.GetExcelSheet<ContentFinderCondition>()
                .GetRowOrDefault(contentFinderConditionRowId)?.ClassJobLevelRequired ?? 0;
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer hub: the sync level for duty #{contentFinderConditionRowId} could not be read.");
        }

        dutyLevels[contentFinderConditionRowId] = level;
        return level;
    }

    /// <summary>The giver's position in the coordinates the game itself prints — "(x11.4, y11.0)" —
    /// or empty when there is no giver, no map, or the map row cannot be read.
    ///
    /// <para>The catalogue stores world coordinates because that is what the router needs. The map
    /// scale that turns them into the numbers on a player's screen lives on the <c>Map</c> sheet, so
    /// the conversion can only happen here, against the installed game.</para></summary>
    public string Coordinates(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        if (unlock.GiverMap is not { } mapId || mapId == 0)
        {
            return string.Empty;
        }

        try
        {
            if (data.GetExcelSheet<Map>().GetRowOrDefault(mapId) is not { } map || map.SizeFactor == 0)
            {
                return string.Empty;
            }

            var x = MapCoords.WorldToMapAxis(unlock.GiverX, map.SizeFactor, map.OffsetX);
            var y = MapCoords.WorldToMapAxis(unlock.GiverZ, map.SizeFactor, map.OffsetY);
            return string.Create(
                CultureInfo.InvariantCulture, $"(x{x:0.0}, y{y:0.0})");
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer hub: no map coordinates for '{unlock.Def.Unlock}'.");
            return string.Empty;
        }
    }

    /// <summary>Whether the game that is actually installed has art at this id.
    ///
    /// <para>The same guard the status icons have, and for the same reason: an id read off a sheet
    /// is a claim about this patch, and a claim that has stopped being true must cost the picture
    /// and nothing else. The page is laid out so a missing banner drops the block whole, so a miss
    /// here is a page without a picture rather than a node cropping a texture that is not
    /// there.</para>
    ///
    /// <para>Never throws, and never asked twice: <see cref="Banner"/> caches the answer, misses
    /// included.</para></summary>
    private bool Drawable(uint iconId)
    {
        if (iconId == 0)
        {
            return false;
        }

        try
        {
            if (textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture)
                && texture.TryGetWrap(out _, out _))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer hub: banner {iconId} could not be checked, so it will not be drawn.");
            return false;
        }

        if (!loggedMissingBanner)
        {
            // Once per session. Every entry that carries a moved id would otherwise say the same
            // thing, and the first line already carries the whole story.
            loggedMissingBanner = true;
            log.Warning(
                $"Wayfarer journal: banner {iconId} does not resolve in this game version, so the entries it "
                + "belongs to are drawn without their picture. Nothing else is affected.");
        }

        return false;
    }

    /// <summary>The duty's own banner. <c>ContentFinderCondition.Image</c> — not <c>Icon</c>, which
    /// turns out to be the 136x168 levequest card on all six rows that carry one.</summary>
    private uint Duty(ResolvedUnlock unlock) =>
        unlock.Def.Reward is { Kind: "ContentFinderCondition" } reward
            ? data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(reward.Id)?.Image ?? 0u
            : 0u;

    /// <summary>The gate quest's own banner — the picture the game draws at the top of that quest's
    /// page in the player's journal, which is the same picture this page is standing in for.
    ///
    /// <para><b>Only when there is a page to draw it on.</b> A quest with no journal entry —
    /// <c>JournalGenre.RowId == 0</c>, the same fact <see cref="Wayfarer.Core.Unlocks.QuestNameCandidate"/>
    /// already tracks to tell a live row from a retired one — has no page, and so no picture at the
    /// top of it. Its <c>Icon</c> field is not "this quest's banner, at a stale id"; it is not a
    /// banner at all, because the row it names was never intended to be seen. Reading it anyway is
    /// what put 100463 on 'Rank 2 Heavensward Daily Hunts': that catalogue entry is gated by 'Better
    /// Bill Hunting', a hidden system quest with no journal presence of its own, and the hunt tier is
    /// not that quest wearing a different name — it is a different thing standing behind the same
    /// gate. Falling back to no banner here is the deliberate miss for entry kinds that genuinely
    /// have none, not a texture lookup this plugin failed to make.</para></summary>
    private uint Quest(ResolvedUnlock unlock)
    {
        if (unlock.QuestRowId is not { } rowId)
        {
            return 0u;
        }

        var quest = data.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(rowId);
        return quest is { JournalGenre.RowId: not 0 } ? quest.Value.Icon : 0u;
    }
}
