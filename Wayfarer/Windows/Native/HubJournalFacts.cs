using System.Globalization;
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
internal sealed class HubJournalFacts(IDataManager data, IPluginLog log)
{
    private readonly Dictionary<uint, uint> banners = [];

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
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer hub: no banner could be resolved for '{unlock.Def.Unlock}'.");
            icon = 0;
        }

        banners[key] = icon;
        return icon;
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

    /// <summary>The duty's own banner. <c>ContentFinderCondition.Image</c> — not <c>Icon</c>, which
    /// turns out to be the 136x168 levequest card on all six rows that carry one.</summary>
    private uint Duty(ResolvedUnlock unlock) =>
        unlock.Def.Reward is { Kind: "ContentFinderCondition" } reward
            ? data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(reward.Id)?.Image ?? 0u
            : 0u;

    /// <summary>The gate quest's own banner — the picture the game draws at the top of that quest's
    /// page in the player's journal, which is the same picture this page is standing in for.
    /// </summary>
    private uint Quest(ResolvedUnlock unlock) =>
        unlock.QuestRowId is { } rowId
            ? data.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(rowId)?.Icon ?? 0u
            : 0u;
}
