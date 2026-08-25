using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The readout's one rule about the game's own bitmap-font glyphs: <b>a subordinate line
/// carries a glyph if and only if it carries an action.</b>
///
/// <para><b>Why a biconditional and not a preference.</b> The glyph is the affordance. Nothing else on
/// a line of the readout's block says the line can be pressed — there is no hand cursor to see from the
/// sofa, no underline, and the words deliberately no longer carry a "(click)" suffix. So a glyph on a
/// line that does nothing invites a press the readout will not answer, and an action on a line with no
/// glyph is a press nobody will ever find. The two are one decision, and
/// <c>ReadoutComposer.Pressable</c> is where it is made.</para>
///
/// <para><b>The subject line is the one exception, and it is asserted rather than skipped.</b> It is
/// the quest name written across the banner's parchment, not a line of the block, and what says the
/// plate can be pressed is the plate — see <see cref="ReadoutLine.Glyph"/>. An icon inside the title
/// would say nothing the parchment does not. So it has an action and no glyph, and
/// <see cref="The_subject_line_is_the_plate_rather_than_a_marked_line"/> pins exactly that shape so
/// the carve-out cannot quietly widen.</para>
///
/// <para><b>Driven off the composer rather than a list.</b> Every case below is real
/// <see cref="ReadoutComposer.Compose"/> output, and the invariant is asserted over every line of
/// every one of them. A line added later with a glyph and no action — or an action and no glyph — fails
/// here without anybody having to remember to add a case for it.</para></summary>
public class ReadoutGlyphTests
{
    private const uint DutyTerritory = 621;
    private const uint DutyInstanceContentId = 258;
    private const uint DutyCfcId = 262;
    private const string DutyName = "The Fist of the Father";

    /// <summary>Every readout the composer can be asked for, named so a failure says which one.</summary>
    public static TheoryData<string> Cases
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in Composed.Keys)
            {
                data.Add(name);
            }

            return data;
        }
    }

    private static IReadOnlyDictionary<string, ReadoutContent> Composed { get; } = BuildCases();

    /// <summary>The rule itself, over every subordinate line of every readout.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_line_has_a_glyph_exactly_when_it_has_an_action(string name)
    {
        foreach (var line in Composed[name].Lines.Where(l => !l.Subject))
        {
            var hasGlyph = line.Glyph != DtrGlyph.None;
            var hasAction = line.Action != ReadoutLineAction.None;
            var message = $"{name}: \"{line.Text}\" has glyph {line.Glyph} and action {line.Action}. "
                + "A glyph is the readout's only way of saying a line can be pressed, so the two go "
                + "together or not at all — see ReadoutComposer.Pressable.";

            Assert.True(hasGlyph == hasAction, message);
        }
    }

    /// <summary>The carve-out, pinned rather than assumed: the subject line takes an action — a press
    /// on the parchment opens the game's Journal — and never a glyph, because it is the plate that
    /// says it can be pressed. At most one line is ever the subject, which is what keeps this an
    /// exception rather than a second rule.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void The_subject_line_is_the_plate_rather_than_a_marked_line(string name)
    {
        var subjects = Composed[name].Lines.Where(l => l.Subject).ToList();

        Assert.InRange(subjects.Count, 0, 1);
        Assert.DoesNotContain(subjects, l => l.Glyph != DtrGlyph.None);
    }

    /// <summary>A glyph is also never dropped somewhere it cannot be seen: the index it is inserted at
    /// has to be inside the words it is inserted into. The drawing layer clamps rather than throwing,
    /// which is right for a per-frame path and wrong to rely on.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_glyphs_position_is_inside_its_own_line(string name)
    {
        foreach (var line in Composed[name].Lines.Where(l => l.Glyph != DtrGlyph.None))
        {
            Assert.InRange(line.GlyphAt, 0, line.Text.Length);
        }
    }

    /// <summary>The duty a player has unlocked: the duty's own name, the duty mark in front of it, and
    /// a press that opens the Duty Finder there. The prose it replaces — "Complete the duty: " — spent
    /// most of a narrow line saying what the mark says at a glance.</summary>
    [Fact]
    public void An_unlocked_duty_is_its_own_name_with_the_duty_mark_and_a_press()
    {
        var line = Assert.Single(
            Composed["duty unlocked"].Lines, l => l.Action == ReadoutLineAction.OpenDutyFinder);

        Assert.Equal(DutyName, line.Text);
        Assert.Equal(DtrGlyph.Duty, line.Glyph);
        Assert.Equal(0, line.GlyphAt);
    }

    /// <summary>The duty a player has NOT unlocked: the whole sentence, and no mark at all. There is
    /// nothing to queue for, and the sentence says something no icon can — that the content has to be
    /// unlocked first.</summary>
    [Fact]
    public void A_locked_duty_keeps_its_words_and_takes_no_mark()
    {
        var line = Assert.Single(Composed["duty locked"].Lines, l => l.Text.Contains(DutyName, StringComparison.Ordinal));

        Assert.Equal($"{DutyObjectiveGuidance.UnlockDutyPrefix}{DutyName}", line.Text);
        Assert.Equal(DtrGlyph.None, line.Glyph);
        Assert.Equal(ReadoutLineAction.None, line.Action);
    }

    /// <summary>The teleport line makes the same distinction, which is what makes it a rule rather than
    /// two decisions that happen to agree: attuned is somewhere the player can go from here, so it
    /// takes the crystal and the press; not attuned is a statement of fact, so it takes neither.
    /// </summary>
    [Fact]
    public void The_teleport_line_is_marked_only_where_the_shard_is_attuned()
    {
        var attuned = Assert.Single(
            Composed["other zone, attuned"].Lines, l => l.Action == ReadoutLineAction.Teleport);
        Assert.Equal(DtrGlyph.Aetheryte, attuned.Glyph);

        // Inline rather than in front: "Teleport to " then the crystal then the shard's name, so the
        // mark reads as part of the sentence rather than as a bullet on it.
        Assert.InRange(attuned.GlyphAt, 1, attuned.Text.Length - 1);

        var locked = Composed["other zone, not attuned"].Lines;
        Assert.DoesNotContain(locked, l => l.Action == ReadoutLineAction.Teleport);
        Assert.DoesNotContain(locked, l => l.Glyph != DtrGlyph.None);
    }

    /// <summary>The hunting summary, stated as the rule rather than as a fixed expectation, because
    /// whether that line becomes pressable is being decided elsewhere. It takes the game's own monster
    /// mark exactly when it takes a press — so the mark arrives with the press and never before it, and
    /// a mark of some other kind fails here either way.</summary>
    [Fact]
    public void The_hunting_summary_takes_the_monster_mark_exactly_when_it_becomes_pressable()
    {
        var line = Assert.Single(
            Composed["hunting summary"].Lines, l => l.Text.Contains("Ornery Karakul", StringComparison.Ordinal));

        Assert.Equal(line.Action != ReadoutLineAction.None, line.Glyph == DtrGlyph.Monster);
    }

    /// <summary>Every glyph the composer can emit has a concrete icon behind it. Enumerated off the
    /// enum rather than listed, so a value added to it without a mapping fails here instead of
    /// silently drawing a line with no mark — which looks exactly like a line that was never meant to
    /// have one.</summary>
    [Fact]
    public void The_drawing_layer_maps_every_glyph_the_composer_can_emit()
    {
        var icon = SourceGuard.Body(
            SourceGuard.SourceOf("Wayfarer/Windows/Native/ReadoutBodyNode.cs"),
            "private static BitmapFontIcon? Icon(DtrGlyph glyph)");

        foreach (var glyph in Enum.GetValues<DtrGlyph>().Where(g => g != DtrGlyph.None))
        {
            Assert.Contains($"DtrGlyph.{glyph} =>", icon, StringComparison.Ordinal);
        }
    }

    /// <summary>The duty name is lifted back out of the reason by the class that wrote it, against its
    /// own named constants — not by a surface pattern-matching on English.</summary>
    [Fact]
    public void The_duty_name_is_recovered_by_whoever_wrote_the_reason()
    {
        Assert.Equal(DutyName, DutyObjectiveGuidance.DutyName($"{DutyObjectiveGuidance.CompleteDutyPrefix}{DutyName}"));
        Assert.Equal(DutyName, DutyObjectiveGuidance.DutyName($"{DutyObjectiveGuidance.UnlockDutyPrefix}{DutyName}"));
        Assert.Null(DutyObjectiveGuidance.DutyName("Head for the Crystarium"));
        Assert.Null(DutyObjectiveGuidance.DutyName(null));
    }

    private static Dictionary<string, ReadoutContent> BuildCases() => new(StringComparer.Ordinal)
    {
        ["idle"] = Compose(new NavigationState { Mode = NavigationState.Modes.Idle }),
        ["same zone"] = Compose(SameZone()),
        ["arrived"] = Compose(SameZone() with { QuestName = "Heroes of the Hour" }, distance: 2f),
        ["search area"] = Compose(SameZone() with { TargetRadiusYalms = 20f }, distance: 40f),
        ["other zone, attuned"] = Compose(OtherZone(attuned: true)),
        ["other zone, not attuned"] = Compose(OtherZone(attuned: false)),
        ["duty unlocked"] = Compose(Duty(unlocked: true)),
        ["duty locked"] = Compose(Duty(unlocked: false)),
        ["hunt engaged"] = Compose(Engaged("Hunting Log - Gladiator")),
        ["unlock route engaged"] = Compose(Engaged("Unlock route") with { IsPickup = true }),
        ["hunting summary"] = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = SameZone(),
            DistanceYalms = 120f,
            HuntingSummary = "Ornery Karakul 2/3",
        }),
        ["nearby unlocks"] = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = SameZone(),
            DistanceYalms = 120f,
            NearbyUnlocks = ["Chocobo racing", "Glamours"],
        }),
        ["reason only"] = Compose(new NavigationState
        {
            Mode = NavigationState.Modes.OtherZone,
            SourceLabel = "Main Scenario",
            QuestName = "Heroes of the Hour",
            Reason = "no route found",
        }),
    };

    private static ReadoutContent Compose(NavigationState state, float? distance = 120f) =>
        ReadoutComposer.Compose(new ReadoutInputs { State = state, DistanceYalms = distance });

    private static NavigationState SameZone() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Main Scenario",
        QuestId = 4321u,
        QuestName = "The Ul'dahn Envoy",
        StepLabel = "Speak with Lucia.",
        TargetX = 12f,
        TargetZ = -40f,
    };

    private static NavigationState OtherZone(bool attuned) => new()
    {
        Mode = NavigationState.Modes.OtherZone,
        SourceLabel = "Main Scenario",
        QuestId = 4321u,
        QuestName = "Heroes of the Hour",
        StepLabel = "Speak with Lucia.",
        ZoneName = "The Pillars",
        EntranceName = "Gates of Judgement",
        EntranceX = 5f,
        EntranceZ = 6f,
        AetheryteName = "Foundation",
        AetheryteId = 70u,
        AetheryteUnlocked = attuned,
    };

    private static NavigationState Engaged(string sourceLabel) => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = sourceLabel,
        Engaged = true,
        QuestName = "Ornery Karakul",
        TargetX = 0f,
        TargetZ = 0f,
    };

    /// <summary>Built by the class that owns the shape rather than hand-written here, so the readout is
    /// composed from exactly what the live guidance would hand it.</summary>
    private static NavigationState Duty(bool unlocked) =>
        DutyObjectiveGuidance.TryBuild(
            targetTerritory: DutyTerritory,
            territoryToDuty: t => t == DutyTerritory
                ? new DutyInfo(DutyName, DutyInstanceContentId, DutyCfcId)
                : null,
            isInstanceContentUnlocked: id => unlocked && id == DutyInstanceContentId,
            displayQuestId: 1000u,
            questName: "Disarmed",
            stepLabel: "Defeat the Oppressor.",
            isPickup: false,
            routeStop: null,
            routeTotal: null)!;
}
