using Dalamud.Plugin.Services;
using Lumina.Excel;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Windows.Native;

/// <summary>The journal's own vocabulary, read out of the running client rather than translated by
/// us.
///
/// <para><b>Why a reference and not a copy.</b> This is the same argument
/// <see cref="GameTextRef"/> makes for requirement prose, applied to the four words the window's
/// section headings need. Square Enix already wrote "Requirements", "Reward", "Description" and
/// "Objectives", already localised them into every language the client ships, and already keeps them
/// current across patches. A copy of the English in our source is a copy that is wrong for every
/// player who is not playing in English and that can go stale — so what is stored here is the
/// <c>Addon</c> row, and the string is fetched at runtime.</para>
///
/// <para><b>Provenance of the row numbers.</b> Read off <c>JournalDetail.uld</c> and its
/// <c>AtkComponentJournalCanvas</c>, where the heading text nodes carry their <c>textid</c>
/// outright: <c>#29</c> is 463, <c>#7</c> is 543, <c>#11</c> is 462, <c>#24</c> is 2835, and the
/// dedicated <c>RequirementsNotMetLabelTextNode</c> <c>#33</c> is 479. Each was then resolved
/// against the installed sheet and the result checked by eye.</para>
///
/// <para><b>Failure is a fallback, not a fault.</b> A sheet that cannot be read costs the
/// localisation and nothing else: the English the plugin has always shipped is returned instead, and
/// the reason is logged once rather than once per heading.</para></summary>
internal sealed class JournalWords(IDataManager data, IPluginLog log)
{
    /// <summary>Addon row 2835 — the heading over what an entry still needs.</summary>
    private const uint RequirementsRow = 2835u;

    /// <summary>Addon row 463 — the heading over what it grants.</summary>
    private const uint RewardRow = 463u;

    /// <summary>Addon row 543 — the heading over the prose.</summary>
    private const uint DescriptionRow = 543u;

    /// <summary>Addon row 462 — the heading over the step you are on.</summary>
    private const uint ObjectivesRow = 462u;

    /// <summary>Addon row 479 — "This quest is not yet available.", the sentence
    /// <c>AddonJournalDetail</c>'s own requirements label is authored with.</summary>
    private const uint NotAvailableRow = 479u;

    private readonly Dictionary<uint, string?> cache = [];
    private bool failureLogged;

    /// <inheritdoc cref="RequirementsRow"/>
    public string Requirements => Word(RequirementsRow, "Requirements");

    /// <inheritdoc cref="RewardRow"/>
    public string Reward => Word(RewardRow, "Reward");

    /// <inheritdoc cref="DescriptionRow"/>
    public string Description => Word(DescriptionRow, "Description");

    /// <inheritdoc cref="ObjectivesRow"/>
    public string Objectives => Word(ObjectivesRow, "Objectives");

    /// <summary>The game's own "not yet available" sentence, or null when the sheet could not be
    /// read. Null rather than a fallback: this one is only ever offered when the thing in the way is
    /// a quest, and our own words for that case read better than a paraphrase of theirs would.
    /// </summary>
    public string? NotAvailable => Resolve(NotAvailableRow);

    private string Word(uint row, string fallback) => Resolve(row) ?? fallback;

    /// <summary>One <c>Addon</c> row, cached — misses included, so a sheet that is missing is walked
    /// once rather than once per window open.</summary>
    private string? Resolve(uint row)
    {
        if (cache.TryGetValue(row, out var cached))
        {
            return cached;
        }

        string? text = null;
        try
        {
            // RawRow rather than a typed wrapper for the same reason UnlockService reads its
            // GameTextRefs that way: one code path for any sheet, and column 0 is Addon's text.
            var sheet = data.Excel.GetSheet<RawRow>(null, "Addon");
            if (sheet.TryGetRow(row, out var value))
            {
                var read = value.ReadStringColumn(0).ExtractText();
                text = string.IsNullOrWhiteSpace(read) ? null : read;
            }
        }
        catch (Exception ex)
        {
            LogOnce(ex);
        }

        cache[row] = text;
        return text;
    }

    private void LogOnce(Exception ex)
    {
        if (failureLogged)
        {
            return;
        }

        failureLogged = true;
        const string why =
            "Wayfarer journal: the game's Addon sheet could not be read, so the journal window's section "
            + "headings are drawn in English rather than in the client's own language.";
        log.Warning(ex, why);
    }
}
