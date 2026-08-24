using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The status vocabulary. The property that matters is not which icon a state gets, it is
/// that <b>no two states are told apart by colour alone</b> — which is the rule the previous row
/// broke, and which nothing in a screenshot review reliably catches.</summary>
public class UnlockStatusDisplayTests
{
    public static TheoryData<UnlockStatus> EveryStatus()
    {
        var data = new TheoryData<UnlockStatus>();
        foreach (var status in Enum.GetValues<UnlockStatus>())
        {
            data.Add(status);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Every_state_has_a_shape_a_word_and_a_sentence(UnlockStatus status)
    {
        var unlock = new ResolvedUnlock { Def = new UnlockDefinition { Unlock = "x" }, Status = status };

        Assert.NotEqual(0u, UnlockStatusDisplay.IconId(status));
        Assert.NotEmpty(UnlockStatusDisplay.Word(status));
        Assert.NotEmpty(UnlockStatusDisplay.Sentence(unlock));
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Every_icon_the_table_can_ask_for_is_one_the_caller_can_validate_up_front(UnlockStatus status)
    {
        // HubStatusIcons resolves lazily per id, but a caller that wants to check the whole
        // vocabulary at startup must be able to enumerate it — a state whose icon is not in
        // AllIcons would slip past that check and fail in the field instead.
        Assert.Contains(UnlockStatusDisplay.IconId(status), UnlockStatusDisplay.AllIcons);
    }

    /// <summary>The direct answer to "does green mean I can do it now?". For any two states the
    /// player is meant to tell apart, <b>something other than the colour</b> has to differ: the
    /// shape, the word, or the sentence. The locked family shares a shape and a word by design —
    /// nine padlocks would be unlearnable — so the sentence is what carries it, and this is the
    /// test that stops the sentence being quietly generalised until it does not.
    ///
    /// <para><c>UnknownGate</c> and <c>RequirementsUnknown</c> are exempt because they are one
    /// state as far as the player is concerned. The distinction is internal: a gate this plugin can
    /// see versus one it merely suspects. Presenting them differently would be presenting a
    /// difference nobody can act on.</para></summary>
    [Fact]
    public void No_two_states_are_distinguished_by_colour_alone()
    {
        var deliberatelyIdentical = new HashSet<UnlockStatus>
        {
            UnlockStatus.UnknownGate, UnlockStatus.RequirementsUnknown,
        };

        foreach (var tone in Enum.GetValues<UnlockStatus>().GroupBy(UnlockStatusDisplay.Tone))
        {
            foreach (var a in tone)
            {
                foreach (var b in tone)
                {
                    if (a == b || (deliberatelyIdentical.Contains(a) && deliberatelyIdentical.Contains(b)))
                    {
                        continue;
                    }

                    Assert.False(
                        SameToThePlayer(a, b),
                        $"{a} and {b} share tone {tone.Key}, icon, word and sentence — only colour tells them apart.");
                }
            }
        }
    }

    /// <summary>Nine distinct padlocks would be unlearnable, so the locked family deliberately
    /// shares one shape. What separates them is the sentence, and it has to actually differ.</summary>
    [Fact]
    public void The_locked_family_shares_a_shape_and_is_separated_by_words()
    {
        var levelLocked = new ResolvedUnlock
        {
            Def = new UnlockDefinition(),
            Status = UnlockStatus.LevelLocked,
            LockReason = "needs level 58",
        };
        var questLocked = new ResolvedUnlock
        {
            Def = new UnlockDefinition(),
            Status = UnlockStatus.QuestLocked,
            LockReason = "needs quest 'Into the Aery'",
        };

        Assert.Equal(
            UnlockStatusDisplay.IconId(UnlockStatus.LevelLocked),
            UnlockStatusDisplay.IconId(UnlockStatus.QuestLocked));

        Assert.Equal("Locked — needs level 58.", UnlockStatusDisplay.Sentence(levelLocked));
        Assert.Equal("Locked — needs quest 'Into the Aery'.", UnlockStatusDisplay.Sentence(questLocked));
    }

    /// <summary>A gate with no reason attached must still say something true. An empty dash after
    /// "Locked —" would be worse than the state word on its own.</summary>
    [Fact]
    public void A_lock_with_no_recorded_reason_still_says_something_true()
    {
        var noReason = new ResolvedUnlock
        {
            Def = new UnlockDefinition(),
            Status = UnlockStatus.LevelLocked,
            QuestLevel = 58,
        };

        Assert.Equal("Locked — needs level 58.", UnlockStatusDisplay.Sentence(noReason));
    }

    [Fact]
    public void Only_a_missed_entry_gets_the_bad_tone()
    {
        foreach (var status in Enum.GetValues<UnlockStatus>())
        {
            var expected = status == UnlockStatus.LockedOut ? UnlockStatusTone.Bad : UnlockStatusTone.Normal;
            if (UnlockStatusDisplay.Tone(status) == UnlockStatusTone.Bad)
            {
                Assert.Equal(UnlockStatusTone.Bad, expected);
            }
        }

        Assert.Equal(UnlockStatusTone.Bad, UnlockStatusDisplay.Tone(UnlockStatus.LockedOut));
    }

    /// <summary>Available loses its green on purpose: the gold marker is the game's own "you can
    /// start this" signal, and the colour channel is needed for complete and missed.</summary>
    [Fact]
    public void Available_reads_as_a_normal_row_carrying_a_marker()
    {
        Assert.Equal(UnlockStatusTone.Normal, UnlockStatusDisplay.Tone(UnlockStatus.Available));
        Assert.Equal(UnlockStatusDisplay.AvailableIcon, UnlockStatusDisplay.IconId(UnlockStatus.Available));
        Assert.Equal(UnlockStatusTone.Dimmed, UnlockStatusDisplay.Tone(UnlockStatus.Done));
    }

    /// <summary>The whole point of <see cref="UnlockStatus.PartnerRequired"/> existing as its own
    /// status rather than reusing <see cref="UnlockStatus.RequirementsUnknown"/>: the word and the
    /// sentence a player actually reads have to say "partner", not the vaguer "requirements
    /// unknown" — one is a gap in this plugin, the other is a gap no plugin can close, and only
    /// the first invites "maybe a future version will know".</summary>
    [Fact]
    public void PartnerRequired_ReadsAsNeedingAPartner_NotAsAnUnknownRequirement()
    {
        var bare = new ResolvedUnlock { Def = new UnlockDefinition(), Status = UnlockStatus.PartnerRequired };
        var withReason = new ResolvedUnlock
        {
            Def = new UnlockDefinition(),
            Status = UnlockStatus.PartnerRequired,
            LockReason = "same Home World, party of two, both wearing a Promise Wristlet, in East Shroud",
        };

        Assert.Contains("partner", UnlockStatusDisplay.Word(UnlockStatus.PartnerRequired), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("partner", UnlockStatusDisplay.Sentence(bare), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown", UnlockStatusDisplay.Sentence(bare), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "Needs a partner — same Home World, party of two, both wearing a Promise Wristlet, in East Shroud.",
            UnlockStatusDisplay.Sentence(withReason));

        // Never Available, and dimmed like every other not-currently-actionable state rather than
        // singled out as an error — a missing partner is not the player's fault the way LockedOut is.
        Assert.NotEqual(UnlockStatusDisplay.AvailableIcon, UnlockStatusDisplay.IconId(UnlockStatus.PartnerRequired));
        Assert.Equal(UnlockStatusTone.Dimmed, UnlockStatusDisplay.Tone(UnlockStatus.PartnerRequired));
    }

    /// <summary>Whether two states are indistinguishable once the colour channel is removed —
    /// evaluated on a bare entry, because a state's own fallback sentence is the one it is
    /// guaranteed to be able to show.</summary>
    private static bool SameToThePlayer(UnlockStatus a, UnlockStatus b) =>
        UnlockStatusDisplay.IconId(a) == UnlockStatusDisplay.IconId(b)
        && string.Equals(UnlockStatusDisplay.Word(a), UnlockStatusDisplay.Word(b), StringComparison.Ordinal)
        && string.Equals(BareSentence(a), BareSentence(b), StringComparison.Ordinal);

    private static string BareSentence(UnlockStatus status) =>
        UnlockStatusDisplay.Sentence(new ResolvedUnlock { Def = new UnlockDefinition(), Status = status });
}
