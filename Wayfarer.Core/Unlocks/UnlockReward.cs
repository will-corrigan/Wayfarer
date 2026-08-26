namespace Wayfarer.Core.Unlocks;

/// <summary>What an entry actually grants, as a sheet identity rather than a name.
///
/// <para><b>Why an identity and not a string.</b> The catalogue's <c>unlock</c> field is prose — it
/// is what a guide calls the thing, and the guide calls the same mount "Firebird (Mount)" and the
/// same duty "The Aery Dungeon Access". Neither string is a row in any sheet, so nothing could be
/// drawn from it: an icon lives on <c>Mount.Icon</c>, not on a sentence. <see cref="Kind"/> names
/// the sheet that owns the identity and <see cref="Id"/> is the row in it, which is the only pair
/// an icon lookup can start from.</para>
///
/// <para><b>Null is a real answer.</b> Most <c>system</c> entries — the Aesthetician, retainer
/// ventures, the gemstone traders — unlock a feature the game has no row for. Those entries carry
/// no reward at all, and the display falls through to what it already draws rather than showing an
/// empty frame. See <c>data/README.md</c>.</para></summary>
/// <param name="Kind">The sheet that owns the identity, spelled exactly as the sheet is —
/// <c>Mount</c>, <c>Companion</c>, <c>Emote</c>, <c>ContentFinderCondition</c>, <c>Orchestrion</c>.
/// The full set is <see cref="UnlockRewardKinds.All"/>.</param>
/// <param name="Id">The row id in that sheet.</param>
/// <param name="Name">The row's own player-facing name, kept so the reward can always be said in
/// words. This is not decoration: KamiToolKit registers tooltips on mouse events only, so an icon
/// with no text beside it is unreadable on a controller.</param>
public sealed record UnlockReward(string Kind, uint Id, string Name);

/// <summary>The closed set of reward kinds the catalogue generator may emit, split by what the game
/// gives each one to draw.
///
/// <para>Shared between the generator (<c>tools/Wayfarer.CatalogueGen</c>), the dataset tests and
/// the drawing code, so the three cannot drift: a kind the generator can write that the display has
/// never heard of is exactly the blank square this field exists to avoid.</para></summary>
public static class UnlockRewardKinds
{
    /// <summary>The thirteen kinds whose own sheet carries an icon column, so the reward draws as
    /// itself: <c>Mount.Icon</c>, <c>Companion.Icon</c>, <c>Emote.Icon</c>,
    /// <c>ContentFinderCondition.Image</c>, <c>Item.Icon</c>, <c>Ornament.Icon</c>,
    /// <c>BeastTribe.Icon</c>, <c>GrandCompanyRank.Icon*</c>, <c>BuddyEquip.Icon*</c>,
    /// <c>Glasses.Icon</c>, <c>CharaMakeCustomize.Icon</c>, <c>ClassJob</c> (through its soul
    /// crystal's item icon), <c>GeneralAction.Icon</c>.</summary>
    public static readonly IReadOnlyList<string> WithIcon =
    [
        "Mount",
        "Companion",
        "Emote",
        "ContentFinderCondition",
        "Item",
        "Ornament",
        "BeastTribe",
        "GrandCompanyRank",
        "BuddyEquip",
        "Glasses",
        "CharaMakeCustomize",
        "ClassJob",
        "GeneralAction",
    ];

    /// <summary>Kinds with no icon column of their own that reach one through the item that grants
    /// them. <c>Orchestrion</c> is the whole list: the sheet has two columns, Name and Description,
    /// and the roll you actually receive is an Item with an ordinary icon.</summary>
    public static readonly IReadOnlyList<string> ViaGrantingItem = ["Orchestrion"];

    /// <summary>Kinds the game ships no usable icon for. Naming them is the point: an entry whose
    /// reward is a title has nothing to draw, and that is a fact about the game rather than a hole
    /// in the catalogue. <c>QuestRewardOther</c> and <c>ContentsNote</c> are here despite having an
    /// icon column, because those columns are not per-reward art — <c>QuestRewardOther</c>'s is 0
    /// for half its rows and <c>ContentsNote</c>'s returns the same dungeon glyph for every row, so
    /// drawing them would say less than saying nothing. <c>SatisfactionNpc</c>'s is a 192x192
    /// portrait, not a slot icon.</summary>
    public static readonly IReadOnlyList<string> WithoutIcon =
    [
        "Title",
        "AetherCurrent",
        "GatheringSubCategory",
        "NotebookDivision",
        "MobHuntOrderType",
        "ContentsNote",
        "QuestRewardOther",
        "SatisfactionNpc",
        "TripleTriadCard",
    ];

    /// <summary>Every kind the generator may emit.</summary>
    public static readonly IReadOnlyList<string> All =
        [.. WithIcon, .. ViaGrantingItem, .. WithoutIcon];

    /// <summary>Whether the display can be expected to draw this kind as a picture. False is not a
    /// failure — see <see cref="WithoutIcon"/>.</summary>
    public static bool DrawsAnIcon(string kind) =>
        WithIcon.Contains(kind, StringComparer.Ordinal)
        || ViaGrantingItem.Contains(kind, StringComparer.Ordinal);

    /// <summary>Whether the generator is allowed to emit this kind at all.</summary>
    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);
}
