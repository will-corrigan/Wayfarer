using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The taxonomy, and the two things that broke when the catalogue stopped being 587
/// entries the same person had written by hand.
///
/// <para>Loads the real shipped <c>data/unlocks-by-level.json</c> — the imported half of it is a
/// function of the installed game data, so a claim about it is worth making against the file rather
/// than against a fixture that would agree with itself.</para></summary>
public class UnlockChannelTests
{
    /// <summary>Every value <c>channel</c> is allowed to hold, kept in step with
    /// <c>ENTRY_CHANNELS</c> in <c>data/unlock-channels.mjs</c>: the channels the coverage policy
    /// lists, plus <c>zone</c> for the entries that open a place the game keeps no row for. Written
    /// out rather than derived, because the point of a closed set is that adding to it is a decision
    /// somebody makes twice.</summary>
    private static readonly HashSet<string> Channels = new(StringComparer.Ordinal)
    {
        "aether-current", "allied-society", "barding", "challenge-log", "chocobo-companion",
        "crafting-log-division", "custom-delivery", "duty", "emote", "facewear",
        "fashion-accessory", "framers-kit", "gathering-folklore", "general-action",
        "grand-company-rank", "hairstyle", "hunt-board", "job", "minion", "mount", "orchestrion",
        "stone-sky-sea", "system", "title", "triple-triad-card", "variant-dungeon", "zone",
    };

    /// <summary>The field the per-category displays will group by. An entry with none, or with one
    /// nothing recognises, is a row that would fall out of whichever page it belongs on — and it
    /// would do so silently, because a display that groups by an unknown key just draws another
    /// group.</summary>
    [Fact]
    public void EveryEntryNamesAChannelTheDisplayCanGroupBy()
    {
        foreach (var d in Load())
        {
            Assert.False(string.IsNullOrEmpty(d.Channel), $"'{d.Unlock}' carries no channel");
            Assert.True(Channels.Contains(d.Channel), $"'{d.Unlock}' has channel '{d.Channel}', which is not a channel");
        }
    }

    /// <summary>The reason the field exists: <c>type</c> cannot answer this question. It has nine
    /// values chosen when the catalogue was duties, systems and a few cosmetics, so the titles, rolls
    /// and jobs it has no word for all land on <c>system</c> — 400-odd entries in one bucket. The
    /// channel splits them, and this is the assertion that says so in numbers rather than in a
    /// comment.</summary>
    [Fact]
    public void TheChannelSaysMoreThanTheTypeDoes()
    {
        var all = Load();
        var systemTyped = all.FindAll(d => string.Equals(d.Type, "system", StringComparison.Ordinal));
        var channels = new HashSet<string>(systemTyped.ConvertAll(d => d.Channel), StringComparer.Ordinal);

        Assert.True(systemTyped.Count > 300, $"only {systemTyped.Count} entries are typed 'system'");
        var message = $"the {systemTyped.Count} entries typed 'system' span only {channels.Count} channels, so "
            + "the taxonomy is not buying anything the type did not already say";
        Assert.True(channels.Count >= 15, message);
    }

    /// <summary>Two different unlocks can share a name and a level. The quest behind "The Promise of
    /// Tomorrow" grants both a title and an orchestrion roll of that name; "Tiisol Ja" is both a
    /// custom-delivery client and that client's crafting-log division. Both pairs are in the shipped
    /// file, and <c>UnlockStatusCalculator</c> groups on (unlock, level, channel) so that neither
    /// pair reports the other's progress — which is what the channel being part of that key is
    /// for.</summary>
    [Fact]
    public void EntriesSharingANameAndLevelAreToldApartByTheirChannel()
    {
        var collisions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var d in Load())
        {
            var key = $"{d.Unlock}|{d.Level?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? d.Category}";
            if (!collisions.TryGetValue(key, out var seen))
            {
                collisions[key] = seen = new HashSet<string>(StringComparer.Ordinal);
            }

            seen.Add(d.Channel);
        }

        var crossChannel = collisions.Where(kv => kv.Value.Count > 1).ToList();
        Assert.NotEmpty(crossChannel);
        foreach (var (key, channels) in crossChannel)
        {
            Assert.True(
                channels.Count > 1,
                $"'{key}' collides within one channel, which the status calculator would treat as one unlock");
        }
    }

    /// <summary>An imported entry has no description, because the sheets state a name and a gate and
    /// no prose, and the row and the reward tray have to survive that. They do, by falling through to
    /// the requirement label and then to the entry's own name — never to a blank line, which renders
    /// as a slightly tall row rather than as an error and is exactly the state the window once
    /// shipped in.</summary>
    [Fact]
    public void AnImportedEntryWithNoDescriptionStillHasSomethingToSay()
    {
        var ungated = new ResolvedUnlock
        {
            Def = new UnlockDefinition
            {
                Unlock = "the Palace of the Dead (Floors 11-20)",
                Channel = "duty",
                Requires = new UnlockRequirement
                {
                    Label = "the game states no unlock quest for this row",
                    Unverifiable = true,
                },
            },
        };
        Assert.Equal("the game states no unlock quest for this row", UnlockRowText.Description(ungated));
        Assert.Equal("the game states no unlock quest for this row", UnlockRowText.GrantedCapability(ungated));

        var bare = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "wind-up brickman", Channel = "minion" },
        };
        Assert.Equal(string.Empty, UnlockRowText.Description(bare));
        Assert.Equal("Wind-up Brickman", UnlockRowText.GrantedCapability(bare));
    }

    /// <summary>The sheets write a whole name in lower case and leave the casing to the client, so a
    /// row imported from one reads "wind-up brickman" until something cases it. Curated prose is left
    /// alone, which is the half of the rule that is easy to get wrong: title-casing everything turns
    /// the catalogue's own "Armoury System: Class change" into "Class Change".</summary>
    [Fact]
    public void SheetNamesAreCasedAndCuratedNamesAreNot()
    {
        Assert.Equal("Wind-up Brickman", Name("wind-up brickman"));
        Assert.Equal("Paladin", Name("paladin"));
        Assert.Equal("Armoury System: Class change", Name("Armoury System: Class change"));

        // The sheet's own lower-case article survives, because that is how the Duty Finder shows it.
        Assert.Equal("the Palace of the Dead (Floors 1-10)", Name("the Palace of the Dead (Floors 1-10)"));

        static string Name(string unlock) =>
            UnlockRowText.Name(new ResolvedUnlock { Def = new UnlockDefinition { Unlock = unlock } });
    }

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
