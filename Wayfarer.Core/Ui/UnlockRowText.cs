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
    /// way.</summary>
    public static string Name(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);
        return unlock.Def.Unlock;
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

    /// <summary>The right-hand gutter: two short tokens and no more. It used to carry three facts
    /// including the state word, which is why none of them were readable — the state is the row's
    /// icon now, and the zone and level are the only things left that vary per row and fit.</summary>
    public static string Trailing(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        var level = LevelToken(unlock);
        var zone = unlock.ZoneName is { Length: > 0 } name ? name : string.Empty;

        if (level.Length == 0)
        {
            return zone;
        }

        return zone.Length == 0 ? level : $"{level} · {zone}";
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
}
