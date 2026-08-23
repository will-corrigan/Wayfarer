namespace Wayfarer.Windows.Native;

/// <summary>Everything the detail pane says about one row.
///
/// <para>Modelled field-for-field on <c>AddonJournalDetail</c>, which is the game's own answer to
/// "tell me about the thing I have selected": a title, a level and category line, a category image,
/// a scrolling body, a dedicated pair of nodes whose only job is to say <b>what requirements are
/// not being met</b>, and an action button. That node list is the content spec — this is not an
/// invented layout, it is the one the people we are imitating already built.</para></summary>
internal sealed class HubRowDetail
{
    /// <summary>The entry's name, in the game's panel-title treatment.</summary>
    public required string Title { get; init; }

    /// <summary>Level and kind, in the game's own words — "Lv 30 · Side quest".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The state's shape, already validated. 0 draws no icon and leans on the sentence.</summary>
    public uint StatusIconId { get; init; }

    /// <summary>One plain sentence naming the state and, when it is locked, what is in the way.
    /// This is what replaces a persistent legend: the key for whatever the cursor is on is always
    /// on screen, and a legend that is always on screen for every state at once is an admission
    /// that the icons failed.</summary>
    public string StatusSentence { get; init; } = string.Empty;

    /// <summary>The catalogue's own description — the paragraph the window has always had and
    /// never shown.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>What is missing, one per line, under the game's own "Requirements not met" label.
    /// Empty when nothing is in the way.</summary>
    public IReadOnlyList<string> Requirements { get; init; } = [];

    /// <summary>Who gives it and where — the giver's name, the zone, the map coordinates. Kept out
    /// of the row's title because it is <i>where you go</i>, not <i>what it is</i>.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>One dimmed line when the catalogue is not sure about this entry. Hidden otherwise:
    /// a provenance note on every row would be noise, and on the rows that need it, it is the most
    /// important thing on the pane.</summary>
    public string Provenance { get; init; } = string.Empty;

    /// <summary>What can be done about it, in order. Buttons are hidden rather than disabled when
    /// they do not apply — the game hides inapplicable rows too, and a greyed button with no
    /// explanation is the shape of the original "nothing in here works" report.</summary>
    public IReadOnlyList<HubDetailAction> Actions { get; init; } = [];
}
