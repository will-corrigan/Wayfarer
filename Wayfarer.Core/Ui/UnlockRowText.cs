using System.Globalization;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Core.Ui;

/// <summary>The words that go on one unlock's row, composed here rather than at the node so the
/// three questions a row has to answer — <i>what is it</i>, <i>what does it give me</i>, <i>where
/// and at what level</i> — can be asserted without a game running.
///
/// <para>The catalogue has carried a plain-English <see cref="UnlockDefinition.Description"/> for
/// every entry since it was written, and the native window never drew it: the row put the name on
/// the left and crammed zone, level and state into a 132px right-hand gutter that ellipsised all
/// three. "I don't know what half of these things are" was that, exactly — a wiring gap, not a
/// data gap. <see cref="Description"/> is the fix and this class exists so it cannot be lost
/// again silently.</para></summary>
public static class UnlockRowText
{
    /// <summary>Line one: what the entry is, and nothing else. The giver's name deliberately does
    /// not appear here — it is <i>where you go</i>, not <i>what it is</i>, and it belongs with the
    /// rest of the directions in the detail pane. The game's own journal titles behave the same
    /// way.
    ///
    /// <para>Title-cased, for the same reason the Hunting Log's monster names are. An entry the
    /// catalogue imported from the game's own sheets carries that sheet's own string, and the sheets
    /// store "wind-up brickman" and "paladin" in lower case and leave the casing to the client.
    /// Doing it here rather than in the data file keeps the committed value equal to the row it came
    /// from — <see cref="DisplayNames.TitleCase"/> is the same transform the Hunting Log uses, and
    /// it leaves anything already carrying a capital exactly as written.</para></summary>
    public static string Name(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);
        return DisplayNames.TitleCase(unlock.Def.Unlock);
    }

    /// <summary>Line two: the catalogue's own sentence about what this unlock gives the player.
    /// Falls back through the editorial note and then the curated requirement label, because a row
    /// with a blank second line is the state this whole change exists to remove — and returns empty
    /// rather than repeating the name, which would read as a rendering fault.</summary>
    public static string Description(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        if (unlock.Def.Description is { Length: > 0 } description)
        {
            return description;
        }

        if (unlock.Def.Notes is { Length: > 0 } notes)
        {
            return notes;
        }

        return unlock.Def.Requires?.Label is { Length: > 0 } label ? label : string.Empty;
    }

    /// <summary>The right-hand caption on line one: the level, and nothing else.
    ///
    /// <para>It used to carry the zone as well, joined by a middle dot, and the two of them did not
    /// fit: the game gives that column 48 pixels (Journal <c>1023 #4</c>) and "Lv 53 · Central
    /// Thanalan" at Axis 12 is four times that, so the engine cut it to "Lv 53…" and the row ended
    /// up truncating a three-character number. A level is the one thing on a row that can be
    /// guaranteed to fit a fixed column, which is why it is the only thing left in it.</para>
    ///
    /// <para>The zone is not lost. Grouping by zone — the checklist's default — puts it on the
    /// section heading directly above the row, and the entry's own page carries it in full beside
    /// the quest giver's name. An ellipsis was never showing it.</para></summary>
    public static string Trailing(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        return unlock.QuestLevel > 0 || unlock.Def.Level is > 0 ? LevelToken(unlock) : string.Empty;
    }

    /// <summary>Just the number — "25" — for the Journal's level badge, or empty when no source
    /// states one.
    ///
    /// <para>Empty rather than a zero or a dash: the badge is hidden when this is empty, and the
    /// entries with no level are a real class — the trophy mounts are gated on owning a set of
    /// other mounts, so any number printed against them would be invented. A blank disc reads as a
    /// failure to load; no disc reads as "this has no level requirement", which is the fact.</para>
    /// </summary>
    public static string LevelNumber(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        if (unlock.QuestLevel > 0)
        {
            return unlock.QuestLevel.ToString(CultureInfo.InvariantCulture);
        }

        return unlock.Def.Level is { } level and > 0
            ? level.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>"Lv 25", or the catalogue's own section name for the handful of entries that have
    /// no level at all — the trophy mounts, whose requirement is a set of other mounts and for
    /// which any printed level would be invented. Never "Lv 0".</summary>
    public static string LevelToken(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        if (unlock.QuestLevel > 0)
        {
            return $"Lv {unlock.QuestLevel.ToString(CultureInfo.InvariantCulture)}";
        }

        return unlock.Def.Level is { } level and > 0
            ? $"Lv {level.ToString(CultureInfo.InvariantCulture)}"
            : unlock.Def.Category ?? string.Empty;
    }

    /// <summary>What a reward-less entry gives you, said as the reward it is: the catalogue's own
    /// opening clause about the capability, because the unlock IS the reward when there is no item
    /// behind it — a duty cleared, a system turned on, a feature switched on. 272 of the 587
    /// shipped entries carry no sheet-backed <see cref="UnlockDefinition.Reward"/> at all — mostly
    /// the 223 <c>system</c> entries — and every one of them still has a real sentence in
    /// <see cref="Description"/>, because the data validators require one (20 to 400 characters,
    /// checked in CI). This is never empty as a result.
    ///
    /// <para>Not the whole sentence: <see cref="Description"/> already has its own line further
    /// down the page, and repeating all of it here would be the same words twice rather than a
    /// headline over a detail. The cut point is the first em dash or sentence end, whichever comes
    /// first — the catalogue's own writers consistently put the noun phrase before either one
    /// ("Unlocks the Glamour system, which lets you—" becomes "Unlocks the Glamour system, which
    /// lets you make one piece of gear look like another." with no dash, so the sentence end wins;
    /// "Adds Guildhests to the Duty Roulette — queue for—" cuts at the dash). A clause with neither
    /// mark, or one so long the cut lands past it anyway, is handed to the caller whole: the tray's
    /// text node ellipsises on overflow, so a long clause costs a fade to "…" and nothing worse.
    /// </para></summary>
    public static string GrantedCapability(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        var description = Description(unlock);
        if (description.Length == 0)
        {
            return unlock.Def.Unlock;
        }

        var sentenceEnd = FirstSentenceEnd(description);
        var dashAt = description.IndexOf('—', StringComparison.Ordinal);

        var cut = dashAt >= 0 && dashAt < sentenceEnd ? dashAt : sentenceEnd;
        return cut > 0 && cut < description.Length ? description[..cut].TrimEnd() : description;
    }

    /// <summary>A duty reward's own line: its name with its sync level appended the way the Duty
    /// Finder states one — "Sastasha (Lv. 15)" — so the tray says enough on its own without
    /// pointing back at the entry's level badge, which is the unlocking QUEST's accept level and is
    /// not always the same number as the duty's own sync level. A level of 0 (the row could not be
    /// read) leaves it off rather than printing "(Lv. 0)", which nothing in the game's own data
    /// means.</summary>
    public static string DutyReward(string dutyName, int dutySyncLevel) =>
        dutySyncLevel > 0
            ? $"{dutyName} (Lv. {dutySyncLevel.ToString(CultureInfo.InvariantCulture)})"
            : dutyName;

    /// <summary>The index just past the first '.', '!' or '?', or the string's own length when it
    /// has none — a clause with no sentence end is not cut by this rule at all.</summary>
    private static int FirstSentenceEnd(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '.' or '!' or '?')
            {
                return i + 1;
            }
        }

        return text.Length;
    }
}
