using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The row's own words. These exist because the failure they guard against is invisible
/// from inside the plugin: a blank second line renders as a slightly tall row, not as an error, and
/// that is precisely the state the window shipped in.</summary>
public class UnlockRowTextTests
{
    [Fact]
    public void The_description_the_catalogue_carries_is_what_the_row_shows()
    {
        var unlock = new ResolvedUnlock
        {
            Def = new UnlockDefinition
            {
                Unlock = "Armoury System: Class change",
                Description = "Lets you swap your active class or job just by changing your equipped weapon.",
                Notes = "an editorial note nobody asked for",
            },
        };

        Assert.Equal("Armoury System: Class change", UnlockRowText.Name(unlock));
        Assert.Equal(
            "Lets you swap your active class or job just by changing your equipped weapon.",
            UnlockRowText.Description(unlock));
    }

    [Fact]
    public void A_description_free_entry_falls_back_rather_than_showing_a_blank_line()
    {
        var withNotes = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "Something", Notes = "the note" },
        };
        var withRequirement = new ResolvedUnlock
        {
            Def = new UnlockDefinition
            {
                Unlock = "Something else",
                Requires = new UnlockRequirement { Label = "seven Extreme trial mounts" },
            },
        };

        Assert.Equal("the note", UnlockRowText.Description(withNotes));
        Assert.Equal("seven Extreme trial mounts", UnlockRowText.Description(withRequirement));
    }

    [Fact]
    public void An_entry_with_nothing_to_say_says_nothing_rather_than_repeating_its_own_name()
    {
        var bare = new ResolvedUnlock { Def = new UnlockDefinition { Unlock = "Bare" } };

        // Repeating the name would read as a rendering fault, which is worse than an empty line.
        Assert.Equal(string.Empty, UnlockRowText.Description(bare));
    }

    /// <summary>The caption column is 48 pixels wide (Journal <c>1023 #4</c>) and the level is the
    /// only thing that fits it. Joining the zone on with a middle dot is what produced the field
    /// report's "Lv 53…" — a three-character number being ellipsised — so nothing may share the
    /// column with it, and neither the zone nor the state word is allowed back in.</summary>
    [Fact]
    public void The_caption_column_carries_the_level_and_nothing_else()
    {
        var unlock = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "Mist Access" },
            QuestLevel = 5,
            ZoneName = "Lower La Noscea",
            Status = UnlockStatus.Available,
        };

        var trailing = UnlockRowText.Trailing(unlock);

        Assert.Equal("Lv 5", trailing);
        Assert.DoesNotContain("Lower La Noscea", trailing, StringComparison.Ordinal);
        Assert.DoesNotContain("Available", trailing, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_with_no_level_leaves_the_caption_column_empty()
    {
        var noZone = new ResolvedUnlock { Def = new UnlockDefinition(), QuestLevel = 30 };
        var noLevel = new ResolvedUnlock { Def = new UnlockDefinition(), ZoneName = "Ul'dah" };
        var neither = new ResolvedUnlock { Def = new UnlockDefinition() };

        Assert.Equal("Lv 30", UnlockRowText.Trailing(noZone));

        // A zone is not a level and never stood in for one: an empty column says "this has no level
        // requirement", which is the fact, where a zone name there said nothing at all.
        Assert.Equal(string.Empty, UnlockRowText.Trailing(noLevel));
        Assert.Equal(string.Empty, UnlockRowText.Trailing(neither));
    }

    /// <summary>The trophy mounts have no level anywhere, and the catalogue's own section name — the
    /// substitute the badge uses — is far too long for a 48-pixel column. The row therefore shows
    /// nothing rather than an ellipsised category masquerading as a level.</summary>
    [Fact]
    public void A_level_less_entry_never_puts_its_category_in_the_level_column()
    {
        var unlock = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "Rose Lanner", Category = "Heavensward Unique Quest Rewards" },
            QuestLevel = 0,
        };

        Assert.Equal("Heavensward Unique Quest Rewards", UnlockRowText.LevelToken(unlock));
        Assert.Equal(string.Empty, UnlockRowText.Trailing(unlock));
    }

    /// <summary>The trophy mounts: no level exists for them anywhere, and the previous row printed
    /// "Lv0" for exactly that reason. The catalogue's own section name is the honest substitute.</summary>
    [Fact]
    public void An_entry_with_no_level_at_all_shows_its_category_and_never_Lv0()
    {
        var unlock = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "Rose Lanner", Category = "Heavensward Unique Quest Rewards" },
            QuestLevel = 0,
        };

        Assert.Equal("Heavensward Unique Quest Rewards", UnlockRowText.LevelToken(unlock));
        Assert.DoesNotContain("Lv 0", UnlockRowText.Trailing(unlock), StringComparison.Ordinal);
    }

    /// <summary>The reward-less majority — 'Rank 2 Heavensward Daily Hunts' among them — get the
    /// capability itself as the reward line, cut at the first clause rather than carrying the whole
    /// description a second time.</summary>
    [Fact]
    public void An_entry_with_no_sheet_reward_states_the_capability_it_grants_instead()
    {
        var dashed = new ResolvedUnlock
        {
            Def = new UnlockDefinition
            {
                Unlock = "Dye System",
                Description = "Unlocks dyeing — apply a dye item to compatible gear to recolor it. Purely cosmetic.",
            },
        };
        var undashed = new ResolvedUnlock
        {
            Def = new UnlockDefinition
            {
                Unlock = "Glamours",
                Description = "Unlocks the Glamour system, which lets you make one piece of gear look like another.",
            },
        };

        Assert.Equal("Unlocks dyeing", UnlockRowText.GrantedCapability(dashed));
        Assert.Equal(
            "Unlocks the Glamour system, which lets you make one piece of gear look like another.",
            UnlockRowText.GrantedCapability(undashed));
    }

    /// <summary>A description with neither an em dash nor sentence-ending punctuation before its
    /// end is handed over whole rather than cut somewhere arbitrary — the tray's own text node
    /// ellipsises the overflow, so nothing is lost by not truncating here as well.</summary>
    [Fact]
    public void A_capability_clause_with_no_natural_cut_point_is_not_truncated_here()
    {
        var unlock = new ResolvedUnlock
        {
            Def = new UnlockDefinition
            {
                Unlock = "Something",
                Description = "Tracks your combined standing across every beast tribe this expansion offers",
            },
        };

        Assert.Equal(
            "Tracks your combined standing across every beast tribe this expansion offers",
            UnlockRowText.GrantedCapability(unlock));
    }

    /// <summary>Never empty: the data validators require a 20-to-400-character description on
    /// every shipped entry, so the fallback to the bare name only matters for a hand-built unlock a
    /// test constructs without one.</summary>
    [Fact]
    public void A_capability_with_no_description_at_all_falls_back_to_the_entrys_own_name()
    {
        var unlock = new ResolvedUnlock { Def = new UnlockDefinition { Unlock = "Bare" } };

        Assert.Equal("Bare", UnlockRowText.GrantedCapability(unlock));
    }

    [Fact]
    public void A_duty_reward_carries_its_own_sync_level_not_the_unlocking_quests()
    {
        Assert.Equal("Sastasha (Lv. 15)", UnlockRowText.DutyReward("Sastasha", 15));
    }

    /// <summary>A duty whose sync level could not be read prints the name alone — "(Lv. 0)" is not
    /// a fact the game states about anything.</summary>
    [Fact]
    public void A_duty_reward_with_no_readable_level_omits_it_rather_than_printing_zero()
    {
        Assert.Equal("Sastasha", UnlockRowText.DutyReward("Sastasha", 0));
    }
}
