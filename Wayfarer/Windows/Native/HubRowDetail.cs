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

    /// <summary>What kind of thing this is, in the game's own word — "Dungeon", "Side quest". The
    /// level used to be part of this string and is now on the badge, where the Journal puts it.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The number for the level badge, or empty when nothing states one. Empty hides the
    /// disc rather than drawing an empty one: the level-less entries — the trophy mounts and the
    /// unique-reward sections — are a real class, and a blank badge would read as a failure to load
    /// rather than as "this has no level requirement".</summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>What this entry actually grants, said in words. Empty when the game states no
    /// reward object for it, which is the ordinary case for a system unlock.</summary>
    public string RewardName { get; init; } = string.Empty;

    /// <summary>The picture of that reward, or 0 when the game ships none — a title, an Aether
    /// Current and a folklore book have no artwork anywhere. The tray and the name are drawn either
    /// way, so 0 reads as "this is what you get" rather than as a slot that failed.</summary>
    public uint RewardIconId { get; init; }

    /// <summary>The size that icon is authored at. An image node's part rectangle has to match it
    /// or the node samples past the edge of the texture — see <see cref="HubRewardIcons"/>.
    /// </summary>
    public System.Numerics.Vector2 RewardIconSize { get; init; }

    /// <summary>The 376x120 piece of art at the top of the journal page's left column, or 0 when
    /// the game ships none for this entry. Drawn only by the page: the strip has no room for it,
    /// which is half the reason the page exists.</summary>
    public uint BannerIconId { get; init; }

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

    /// <summary>The giver's position in the numbers the game prints on its own map — "(x11.4,
    /// y11.0)". Empty when there is no giver or no map. The page's Information section carries it;
    /// the strip has no room and would only ellipsise it away.</summary>
    public string Coordinates { get; init; } = string.Empty;

    /// <summary>The quest that grants this, by name. The page says it in the Information section
    /// rather than in the title: the quest is <i>how you get it</i> and the unlock is <i>what it
    /// is</i>, and a player looking for "Chocobo Mount Access" is not looking for "My Little
    /// Chocobo".</summary>
    public string QuestName { get; init; } = string.Empty;

    /// <summary>One dimmed line when the catalogue is not sure about this entry. Hidden otherwise:
    /// a provenance note on every row would be noise, and on the rows that need it, it is the most
    /// important thing on the pane.</summary>
    public string Provenance { get; init; } = string.Empty;

    /// <summary>What can be done about it, in order. Buttons are hidden rather than disabled when
    /// they do not apply — the game hides inapplicable rows too, and a greyed button with no
    /// explanation is the shape of the original "nothing in here works" report.</summary>
    public IReadOnlyList<HubDetailAction> Actions { get; init; } = [];
}
