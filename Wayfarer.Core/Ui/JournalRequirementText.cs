namespace Wayfarer.Core.Ui;

/// <summary>Saying what is in the way, once.
///
/// <para><b>The defect this exists to make impossible.</b> An entry's lock reason reached the screen
/// twice: <see cref="UnlockStatusDisplay.Sentence"/> folds it into the state line ("Locked — needs
/// gladiator or pugilist or…") and the requirements block lists the same thing underneath, because
/// the block falls back to the same lock reason when it has no itemised list. On the entry whose gate
/// names thirty jobs that is the same four-line sentence printed twice, one under the other, which
/// is exactly what the field report showed.</para>
///
/// <para><b>The rule.</b> Whichever block is going to carry the requirements carries them alone. If
/// there is a requirements block to draw, the state line says only the state — the game's own habit:
/// <c>AddonJournalDetail</c> draws a marker and puts the words in one dedicated pair of nodes
/// (<c>#33</c>/<c>#34</c>), never in both. If there is no requirements block, the state line carries
/// the whole sentence, because then it is the only thing that can.</para></summary>
public static class JournalRequirementText
{
    /// <summary>The state line. <paramref name="word"/> is the one-word state
    /// (<see cref="UnlockStatusDisplay.Word"/>) and <paramref name="sentence"/> the full one
    /// (<see cref="UnlockStatusDisplay.Sentence"/>); the requirements block having anything in it is
    /// what decides between them.</summary>
    public static string StatusLine(string word, string sentence, bool requirementsShown)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(sentence);

        if (!requirementsShown || word.Length == 0)
        {
            return sentence;
        }

        return word.EndsWith('.') ? word : word + ".";
    }

    /// <summary>The heading over the requirements block, preferring the game's own word for it.
    ///
    /// <para><paramref name="gameWord"/> is what <c>Addon</c> row 2835 resolved to in the player's
    /// own client language, through the same
    /// <see cref="Wayfarer.Core.Unlocks.GameTextRef"/> mechanism the catalogue uses for requirement
    /// prose: a reference to Square Enix's own string rather than a copy of it, so it is already
    /// localised and cannot drift out of date with a patch. Null or blank means the sheet could not
    /// be read, and the English fallback is what the plugin has always shipped.</para></summary>
    public static string RequirementsHeading(string? gameWord) =>
        string.IsNullOrWhiteSpace(gameWord) ? "Requirements" : gameWord;

    /// <summary>The lead-in sentence over an itemised requirement list, when the game has one for
    /// this shape of gate.
    ///
    /// <para><paramref name="gameSentence"/> is <c>Addon</c> row 479 — "This quest is not yet
    /// available." — which is the string <c>AddonJournalDetail</c>'s own
    /// <c>RequirementsNotMetLabelTextNode</c> is authored with. It is offered only when the thing in
    /// the way really is a quest: printing it over a duty's or a mount's requirements would be the
    /// game's words applied to something they are not about, which is worse than our own.</para>
    /// </summary>
    public static string? NotMetLead(string? gameSentence, bool gatedByQuest) =>
        !gatedByQuest || string.IsNullOrWhiteSpace(gameSentence) ? null : gameSentence;
}
