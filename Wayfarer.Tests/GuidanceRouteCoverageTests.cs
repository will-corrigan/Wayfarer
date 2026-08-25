using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Whether every action Wayfarer offers still has a route to it, in every state the player
/// can be in — and therefore which entries in the game's own right-click menu are safe to drop
/// because the readout already offers the same thing.
///
/// <para><b>Why this exists.</b> The readout grew its own controls — a settings cog, a follow
/// switcher on the plate's cap, the plate itself opening the Journal, a clickable teleport line — and
/// the obvious tidy-up is to drop the duplicates from the game's context menu. The trap is that
/// "the readout has a control for it" is not the same claim as "the player can reach it": the
/// readout can be switched off, hidden in combat and hidden in duties; its controls exist only on the
/// host that takes input, and the read-only fallback host has none of them; and a control that is on
/// screen can still be wired to a handler that does nothing in the mode the player is actually in.
/// The plate is the worked example — it takes a press and opens the Journal at
/// <c>NavigationState.QuestId</c>, and a hunting target has no quest id — which is why this matrix is
/// keyed on what is being followed as well as on whether the readout is up.</para>
///
/// <para><b>What is asserted, and what is only declared.</b> The readout's Journal route is
/// <i>measured</i>: <see cref="ReadoutComposer"/> is the thing that decides whether the plate carries
/// <see cref="ReadoutLineAction.OpenJournal"/> at all, so the tests below ask it rather than assuming.
/// The rest of the matrix is declared, and deliberately declared pessimistically — an action the
/// readout might offer under some further condition this matrix does not carry counts as not offered,
/// because the only direction an error here can be safe in is "kept an entry that was not
/// needed".</para></summary>
public class GuidanceRouteCoverageTests
{
    /// <summary>How much of the readout is on screen and can be pressed.</summary>
    private enum Surface
    {
        /// <summary>Switched off, hidden in combat, hidden in a duty, or nothing to say.</summary>
        Hidden,

        /// <summary>On screen but read-only — the click-through overlay that stands in when the
        /// readout's own host cannot be built. The words and the arrow, and no hit boxes at
        /// all.</summary>
        ReadOnly,

        /// <summary>On screen with its own host, so its four controls take input.</summary>
        Operable,
    }

    /// <summary>What Wayfarer is being asked to guide the player to. Not interchangeable: a followed
    /// quest has a row in the Quest sheet and a hunting target does not, and that difference decides
    /// whether the plate's press means anything.</summary>
    private enum Subject
    {
        Nothing,
        Quest,
        HuntingTarget,
        UnlockRoute,
    }

    /// <summary>Every action the two menus offer between them.</summary>
    private enum Act
    {
        SwitchFollow,
        OpenJournal,
        OpenSettings,
        Teleport,
        DutyFinder,
        StartHunting,
        StartUnlockRoute,
        Stop,
        OpenUnlocks,
        OpenHuntingLog,
    }

    /// <summary>The whole matrix. Every action, in every readout state, against everything that can
    /// be followed.</summary>
    public static TheoryData<int, int, int> Cells
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (var act in Enum.GetValues<Act>())
            {
                foreach (var surface in Enum.GetValues<Surface>())
                {
                    foreach (var subject in Enum.GetValues<Subject>())
                    {
                        data.Add((int)act, (int)surface, (int)subject);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>The point of the whole file: nothing is ever unreachable. Runs over every cell rather
    /// than over a chosen few, because the cells that strand an action are exactly the ones nobody
    /// thought to choose.</summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void Every_action_has_a_working_route_in_every_state(int act, int surface, int subject)
    {
        var a = (Act)act;
        var s = (Surface)surface;
        var f = (Subject)subject;

        Assert.True(
            ReadoutOffers(a, s, f) || ContextMenuOffers(a, s, f),
            $"{a} has no route with the readout {s} and {f} being followed.");
    }

    /// <summary>The trim rule itself: an entry may only be dropped from the game's menu where the
    /// readout is proven to offer the same action in that same state. Asserted as an implication over
    /// the whole matrix so the rule cannot be satisfied by a cell nobody enumerated.</summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void Nothing_is_dropped_from_the_game_menu_without_the_readout_offering_it(
        int act, int surface, int subject)
    {
        var a = (Act)act;
        var s = (Surface)surface;
        var f = (Subject)subject;

        if (!ContextMenuOffers(a, s, f))
        {
            var message = $"{a} was dropped from the game's menu with the readout {s} and {f} being "
                + "followed, and the readout does not offer it there either.";
            Assert.True(ReadoutOffers(a, s, f), message);
        }
    }

    /// <summary>A readout that is not there cannot be the route to anything, so no entry may be
    /// dropped on account of it. This is the half of the rule that a condition keyed only on "is the
    /// readout visible" gets right — and the next test is the half it gets wrong.</summary>
    [Fact]
    public void A_hidden_or_read_only_readout_offers_nothing_at_all()
    {
        foreach (var act in Enum.GetValues<Act>())
        {
            foreach (var subject in Enum.GetValues<Subject>())
            {
                Assert.False(ReadoutOffers(act, Surface.Hidden, subject));
                Assert.False(ReadoutOffers(act, Surface.ReadOnly, subject));
            }
        }
    }

    /// <summary>The half that a visibility-only condition gets wrong, and the reason this matrix has
    /// a second axis. The switcher and the plate are on screen in every mode; what they do is not the
    /// same in every mode, and an entry dropped on their account is dropped for the mode where they
    /// do nothing too.</summary>
    [Fact]
    public void An_operable_readout_still_offers_different_things_in_different_modes()
    {
        Assert.True(ReadoutOffers(Act.OpenJournal, Surface.Operable, Subject.Quest));
        Assert.False(ReadoutOffers(Act.OpenJournal, Surface.Operable, Subject.HuntingTarget));
        Assert.False(ReadoutOffers(Act.OpenJournal, Surface.Operable, Subject.UnlockRoute));
        Assert.False(ReadoutOffers(Act.OpenJournal, Surface.Operable, Subject.Nothing));
    }

    /// <summary>What the matrix adds up to today, stated once so it is a finding rather than a shrug:
    /// for every action there is at least one state in which the game's context menu is the only route
    /// to it. That is why nothing has been dropped from it. When this stops being true of some action
    /// — when the readout's controls work in every mode — this test is the one that says so, and the
    /// entry can then go.</summary>
    [Fact]
    public void The_game_menu_is_the_only_route_to_every_action_in_at_least_one_state()
    {
        foreach (var act in Enum.GetValues<Act>())
        {
            var stranded = Enum.GetValues<Surface>()
                .SelectMany(s => Enum.GetValues<Subject>().Select(f => (Surface: s, Subject: f)))
                .Any(cell => !ReadoutOffers(act, cell.Surface, cell.Subject));

            var message = $"{act} is now offered by the readout in every state, so its entry in the "
                + "game's own right-click menu is genuinely redundant and this matrix should say so.";
            Assert.True(stranded, message);
        }
    }

    /// <summary>The measured half of the matrix. The plate's press is the Journal only when the
    /// composer gives the subject line <see cref="ReadoutLineAction.OpenJournal"/>, and that is
    /// decided here rather than in the node — so this is the fact the matrix's
    /// <see cref="Act.OpenJournal"/> row rests on, taken from the composer rather than asserted about
    /// it.</summary>
    [Fact]
    public void The_plate_carries_the_journal_action_only_while_a_quest_is_followed()
    {
        Assert.True(HasJournalAction(FollowedQuest()));
        Assert.False(HasJournalAction(EngagedHunt()));
        Assert.False(HasJournalAction(EngagedUnlockRoute()));
        Assert.False(HasJournalAction(Idle()));
    }

    /// <summary>And the corollary the readout cannot show: with no journal action on the plate there
    /// is no journal route on the readout at all, whatever is on screen. The line's own action is the
    /// only thing that decides it.</summary>
    [Fact]
    public void A_followed_hunt_puts_no_journal_action_on_any_line_of_the_readout()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs { State = EngagedHunt() });

        Assert.DoesNotContain(content.Lines, line => line.Action == ReadoutLineAction.OpenJournal);
    }

    /// <summary>The tripwire. Neither menu renderer, nor the single source they both render, may grow
    /// a condition on the readout's own state without this matrix growing with it — because the
    /// source they share also feeds the readout's own subcommand menu, so a condition put there would
    /// strip the very surface it was crediting.</summary>
    [Fact]
    public void Neither_menu_conditions_its_entries_on_the_readouts_state()
    {
        var needles = new[]
        {
            "UseNativeReadout",
            "ArrowHideInCombat",
            "ArrowHideInDuties",
            "ShouldShow",
            "ReadoutFeed",
        };

        var actions = SourceGuard.SourceOf("Wayfarer/GuidanceActions.cs");
        var submenu = SourceGuard.Body(
            SourceGuard.SourceOf("Wayfarer/ContextMenuActions.cs"), "private List<IMenuItem> BuildSubmenuItems()");

        foreach (var needle in needles)
        {
            var message = $"'{needle}' now decides what a menu offers. Any trim keyed on the readout's "
                + "state has to be reflected in GuidanceRouteCoverageTests' matrix first, or an action "
                + "can be dropped in a state where the readout does not offer it either.";

            Assert.DoesNotContain(needle, actions, StringComparison.Ordinal);
            Assert.False(submenu.Contains(needle, StringComparison.Ordinal), message);
        }
    }

    /// <summary>The two exits the matrix leans on hardest: the way out of an engaged route, and the
    /// way back to the main scenario. Both are in the shared source, and both are unconditional
    /// there.</summary>
    [Fact]
    public void The_shared_source_still_offers_the_way_out_and_the_way_back()
    {
        var actions = SourceGuard.SourceOf("Wayfarer/GuidanceActions.cs");

        Assert.Contains("\"Stop\"", actions, StringComparison.Ordinal);
        Assert.Contains("\"Main Scenario\"", actions, StringComparison.Ordinal);
        Assert.Contains("\"Open Settings\"", actions, StringComparison.Ordinal);
    }

    /// <summary>What the readout offers, and it is only ever its own controls: the cog, the switcher on
    /// the plate's cap, the plate itself, and its pressable lines.
    ///
    /// <para>The subcommand menu the plate drops is deliberately not counted. It is the game's context
    /// menu rendering the same shared source the right-click menu renders, so counting it would let
    /// that menu justify trimming itself.</para>
    ///
    /// <para>Nor are the pressable lines counted — the teleport advice, and the duty line that opens
    /// the Duty Finder. Each is on the readout only in a situation this matrix has no axis for: a
    /// teleport being recommended with the click-to-teleport setting on, an objective inside a duty the
    /// player has actually unlocked. Neither is treated as covering a cell, which is what keeps both
    /// entries in the game's own menu unconditionally.</para></summary>
    private static bool ReadoutOffers(Act act, Surface surface, Subject subject)
    {
        if (surface != Surface.Operable)
        {
            return false;
        }

        return act switch
        {
            // The cog, which is on the header pill whatever is being followed.
            Act.OpenSettings => true,

            // The plate. Measured, not assumed — see the composer tests above.
            Act.OpenJournal => subject == Subject.Quest,

            // The switcher cap. Its hit box is on the plate in every mode, but its press has been
            // reported doing nothing while a hunt or an unlock route is engaged, and nothing in the
            // readout's own code proves otherwise — so those two modes count as not offered until
            // they do.
            Act.SwitchFollow => subject is Subject.Quest or Subject.Nothing,

            // Everything else has no control on the readout at all.
            _ => false,
        };
    }

    /// <summary>What the game's own right-click menu offers, which is everything — every entry, in
    /// every state. That is the behaviour this matrix exists to justify keeping.</summary>
    private static bool ContextMenuOffers(Act act, Surface surface, Subject subject) => true;

    private static bool HasJournalAction(NavigationState state) =>
        ReadoutComposer.Compose(new ReadoutInputs { State = state })
                       .Lines.Any(line => line.Action == ReadoutLineAction.OpenJournal);

    private static NavigationState FollowedQuest() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Main Scenario",
        QuestId = 4321u,
        QuestName = "Heroes of the Hour",
        StepLabel = "Speak with Lucia.",
        TargetX = 12f,
        TargetZ = -40f,
    };

    private static NavigationState EngagedHunt() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Hunting Log - Gladiator",
        Engaged = true,
        QuestName = "Ornery Karakul",
        TargetX = 0f,
        TargetZ = 0f,
    };

    private static NavigationState EngagedUnlockRoute() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Unlock route",
        Engaged = true,
        IsPickup = true,
        QuestName = "Chocobo racing",
        TargetX = 0f,
        TargetZ = 0f,
    };

    private static NavigationState Idle() => new() { Mode = NavigationState.Modes.Idle };
}
