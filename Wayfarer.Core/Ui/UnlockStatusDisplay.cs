using Wayfarer.Core.Unlocks;

namespace Wayfarer.Core.Ui;

/// <summary>The status vocabulary: a shape, a sentence, and a colour, in that order of importance.
///
/// <para><b>Why the icon comes first.</b> The player asked "does green mean I can do it now and
/// route there?" — and there was no way to find out, because the state was a colour plus the third
/// token in an ellipsised 132px gutter. The game does not ask anyone to learn that green means
/// available: it draws a marker, a checkmark, a padlock. Colour is a second channel on top of a
/// shape and never the only one, which is also the ten-foot rule ("avoid subtle colour-only
/// distinctions"). Every state below is distinguishable with the colour channel removed
/// entirely.</para>
///
/// <para><b>Why every locked state shares one padlock.</b> Nine distinct lock icons would be
/// unlearnable. The <i>reason</i> is the differentiator and it lives in the sentence, in words —
/// which is the model <c>AddonJournalDetail</c> uses, with one "requirements not met" node and
/// prose inside it.</para>
///
/// <para><b>Provenance of the ids.</b> Extracted from the live install and inspected: the 71000
/// block is 32x32, one family of six per ten (marker, circular arrows, alt marker, dot, green
/// check, blue "!"), corroborated by the <c>EventIconType</c> sheet's row 3
/// (<c>MapIconAvailable = 71000</c>, <c>IconRange = 6</c>). The 60640 block is 24x24 and composites
/// a padlock or a prohibition sign onto a content-type icon.</para></summary>
public static class UnlockStatusDisplay
{
    /// <summary>Gold marker — the game's own "you can start this" signal, the same shape it draws
    /// over a quest giver's head and on the map.</summary>
    public const uint AvailableIcon = 71001;

    /// <summary>Gold in-progress marker.</summary>
    public const uint InProgressIcon = 71003;

    /// <summary>Green check.</summary>
    public const uint CompleteIcon = 71005;

    /// <summary>Blue "!" circle — the game's informational marker, used here for the two states
    /// that are honestly "Wayfarer does not know".</summary>
    public const uint InformationalIcon = 71006;

    /// <summary>Quest scroll with a closed padlock.</summary>
    public const uint LockedQuestIcon = 60645;

    /// <summary>Duty icon with a closed padlock.</summary>
    public const uint LockedDutyIcon = 60641;

    /// <summary>Duty icon with a red prohibition sign — permanently unobtainable.</summary>
    public const uint MissedIcon = 60647;

    /// <summary>Every id this table can ask for, so a caller can validate the whole set once at
    /// startup rather than discovering a bad one row by row.</summary>
    public static IReadOnlyList<uint> AllIcons { get; } =
    [
        AvailableIcon, InProgressIcon, CompleteIcon, InformationalIcon,
        LockedQuestIcon, LockedDutyIcon, MissedIcon,
    ];

    /// <summary>The shape for a state. Every locked flavour except the duty gate resolves to the
    /// same padlock on purpose.</summary>
    public static uint IconId(UnlockStatus status) => status switch
    {
        UnlockStatus.Available => AvailableIcon,
        UnlockStatus.Accepted => InProgressIcon,
        UnlockStatus.Done => CompleteIcon,
        UnlockStatus.LockedOut => MissedIcon,
        UnlockStatus.InstanceLocked => LockedDutyIcon,
        UnlockStatus.UnknownGate or UnlockStatus.RequirementsUnknown or UnlockStatus.Unverified => InformationalIcon,
        _ => LockedQuestIcon,
    };

    /// <summary>The one word for a state. Only used when the icon could not be drawn — see
    /// <see cref="Sentence"/> for what the player normally reads.</summary>
    public static string Word(UnlockStatus status) => status switch
    {
        UnlockStatus.Available => "Available",
        UnlockStatus.Accepted => "In progress",
        UnlockStatus.Done => "Complete",
        UnlockStatus.LockedOut => "Missed",
        UnlockStatus.UnknownGate or UnlockStatus.RequirementsUnknown => "Unknown",
        UnlockStatus.Unverified => "Unverified",
        _ => "Locked",
    };

    /// <summary>Colour as reinforcement only.
    ///
    /// <para><c>Available</c> deliberately loses its green. The gold marker is the game's own
    /// "you can start this" signal and is instantly recognisable; keeping the text green as well
    /// spends a channel that <c>Complete</c> and <c>Missed</c> need, and it is what made the
    /// player have to ask what green meant. Available rows now read as <i>normal</i> rows with a
    /// marker, which is exactly how they read on the game's own map.</para></summary>
    public static UnlockStatusTone Tone(UnlockStatus status) => status switch
    {
        UnlockStatus.Available or UnlockStatus.Accepted => UnlockStatusTone.Normal,
        UnlockStatus.LockedOut => UnlockStatusTone.Bad,
        _ => UnlockStatusTone.Dimmed,
    };

    /// <summary>The plain sentence for a row, with whatever is in the way stated in words. This is
    /// what replaces a persistent legend: the detail pane always carries the sentence for whatever
    /// the cursor is on, so the key is per-row and never competes with content.</summary>
    public static string Sentence(ResolvedUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        return unlock.Status switch
        {
            UnlockStatus.Available => "Available — you can do this now.",
            UnlockStatus.Accepted => "In progress — you have accepted this.",
            UnlockStatus.Done => "Complete.",
            UnlockStatus.LockedOut => "Missed — this can no longer be started on this character.",
            UnlockStatus.Unverified =>
                "Unverified — Wayfarer found this in its catalogue but could not confirm it in game data.",
            UnlockStatus.UnknownGate or UnlockStatus.RequirementsUnknown => UnknownSentence(unlock),
            _ => LockedSentence(unlock),
        };
    }

    private static string UnknownSentence(ResolvedUnlock unlock) =>
        unlock.LockReason is { Length: > 0 } reason
            ? $"Wayfarer can't tell what this needs — {reason}."
            : "Wayfarer can't tell what this needs.";

    // The calculator already phrases every gate as a verb phrase ("needs level 15", "requires
    // clearing Sastasha"), so this reads as one sentence rather than two glued together. The
    // per-status fallbacks exist because a reason is the calculator's to supply and its absence
    // must degrade to something true rather than to an empty dash.
    private static string LockedSentence(ResolvedUnlock unlock)
    {
        if (unlock.LockReason is { Length: > 0 } reason)
        {
            return $"Locked — {reason}.";
        }

        return unlock.Status switch
        {
            UnlockStatus.LevelLocked when unlock.QuestLevel > 0 => $"Locked — you need level {unlock.QuestLevel}.",
            UnlockStatus.QuestLocked => "Locked — an earlier quest has to be finished first.",
            UnlockStatus.InstanceLocked => "Locked — a duty has to be cleared first.",
            UnlockStatus.GrandCompanyLocked => "Locked — it needs Grand Company rank.",
            UnlockStatus.BeastTribeLocked => "Locked — it needs beast tribe reputation.",
            UnlockStatus.MountLocked => "Locked — it needs a mount you do not have.",
            UnlockStatus.CollectionLocked => "Locked — you need several other things first.",
            _ => "Locked — you cannot start this yet.",
        };
    }
}
