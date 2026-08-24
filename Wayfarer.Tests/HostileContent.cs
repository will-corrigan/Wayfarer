namespace Wayfarer.Tests;

/// <summary>The worst content the catalogue and the game's own sheets can actually produce, in one
/// place, so every layout proof is run against the same real fixtures rather than against a plausible
/// invention.
///
/// <para>Each of these is the specific thing a field report was about. The thirty-job enumeration is
/// the requirement sentence the plugin used to generate before it learned to read the category's own
/// name (<c>JobGateText</c>) — kept here <b>after</b> the fix, because the layout still has to survive
/// a string of that shape however it arrives. The longest description is the longest
/// <c>description</c> in <c>data/unlocks-by-level.json</c>, and the two-line title is the entry whose
/// name wrapped over the word "Locked." in the screenshot that started this work.</para></summary>
internal static class HostileContent
{
    /// <summary>The longest <c>description</c> in the shipped catalogue, verbatim — 239 characters,
    /// "Ceremony of Eternal Bonding". Not the longest string in the file (a <c>notes</c> field is
    /// longer) but the longest one the page ever draws as prose.</summary>
    public const string LongestDescription =
        "Unlocks in-game weddings — the Ceremony of Eternal Bonding lets two players hold a formal wedding "
        + "ceremony with exclusive attire and rewards. Always needs a partner, present with you, at the same "
        + "time; there is no way to do it by yourself.";

    /// <summary>The title from the screenshot: long enough to wrap to two lines of Axis 18 in the
    /// page's column, which is what collided with the state line underneath it.</summary>
    public const string TwoLineTitle = "Amh Araeng Aether Current";

    /// <summary>The longest <c>unlock</c> name in the shipped catalogue — a title that wraps well
    /// past two lines, which the page has to bound rather than let run.</summary>
    public const string LongestTitle =
        "Mount Speed increase to all lower level areas: middle La Noscea, lower La Noscea, Central Shroud, "
        + "East Shroud, western Thanalan, central Thanalan;";

    /// <summary>Every job the game's "any combat job" mask flags, in sheet order — the members the
    /// plugin used to print instead of the category's own name.</summary>
    public static readonly string[] EveryCombatJob =
    [
        "gladiator", "pugilist", "marauder", "lancer", "archer", "conjurer", "thaumaturge",
        "paladin", "monk", "warrior", "dragoon", "bard", "white mage", "black mage", "arcanist",
        "summoner", "scholar", "rogue", "ninja", "machinist", "dark knight", "astrologian",
        "samurai", "red mage", "gunbreaker", "dancer", "reaper", "sage", "viper", "pictomancer",
    ];

    /// <summary>The requirement sentence that enumeration produced. 300-odd characters, five lines of
    /// Axis 14 in the journal page's column, and the reason every block under it used to move.
    /// </summary>
    public static string ThirtyJobRequirement =>
        $"needs {string.Join(" or ", EveryCombatJob)} 70";
}
