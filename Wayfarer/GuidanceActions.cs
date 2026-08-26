using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer;

/// <summary>Every action the readout and the game's right-click menu offer, in the order they are
/// offered, decided once.
///
/// <para><b>Why this exists.</b> There are now two menus onto the same set of actions — the game's
/// own right-click menu, and the one the readout's plate drops when it is asked for subcommands —
/// and a third surface, the window's Following tab, that must agree about what can be followed. Written
/// twice, they drift: a condition tightened in one place, a count in a label that stops matching,
/// an entry that exists on one surface and not the other. So the conditions and the words live here
/// and the two menus are renderers.</para>
///
/// <para><b>Hidden, not disabled.</b> Every action that does not apply right now is absent: no
/// teleport suggestion, no routable unlock, nothing engaged to stop. That is the rule the game's
/// menu already followed and the readout's menu inherits it. The one exception is the follow list,
/// where a choice is listed even when it has nothing to start, because a choice that vanishes cannot
/// be learned — that rule lives with the follow choices themselves, and so does the guarantee that
/// every one of them still does something when it is pressed.</para>
///
/// <para><b>What is never absent.</b> <see cref="Subject"/> and the Main Scenario entry. Those two
/// are the readout's answers to "what am I doing" and "get me out of this", and both used to be
/// conditional: the first on the followed thing having a Journal page, the second on nothing being
/// engaged. A hunt has neither property, which is how a controller player ended up on a readout whose
/// plate did nothing and whose way home was greyed out.</para>
///
/// <para>Every list is rebuilt at the moment a menu opens, never cached: a player who opens a menu,
/// walks into another zone and then confirms must not act on what was true when they opened
/// it.</para></summary>
internal sealed class GuidanceActions(
    ModuleRegistry modules,
    QuestHelperConfig cfg,
    IObjectTable objects,
    IClientState clientState,
    Action openSettings,
    Action openFollowing,
    IPluginLog log)
{
    /// <summary>The one word for going back to the default loop, on every surface that offers it.
    /// </summary>
    private const string MainScenarioLabel = "Main Scenario";

    /// <summary>The guidance the actions read and drive, or null when Quest Helper is switched off —
    /// in which case there is nothing to offer at all, since every action below ultimately reads or
    /// drives it.</summary>
    public QuestNavigator? Navigator =>
        modules.Get<QuestHelperModule>() is { Enabled: true } questHelper ? questHelper.Navigator : null;

    /// <summary>What to do about where the player is going right now: take the teleport the readout
    /// is recommending, queue the duty its objective is inside, or stop what is running. With
    /// nothing running, the two ways to start something take Stop's place.</summary>
    public IReadOnlyList<GuidanceAction> Route()
    {
        var actions = new List<GuidanceAction>();
        if (Navigator is not { } navigator)
        {
            return actions;
        }

        var state = navigator.Current;
        Add(actions, Teleport(state));
        Add(actions, DutyFinder(state));

        // The universal exit, shown whenever anything is engaged — a chained unlock route, a single
        // unlock pickup or a hunt alike, since ClearPickup() is the one release valve for all three.
        if (state.Engaged)
        {
            actions.Add(new GuidanceAction("Stop", navigator.ClearPickup));
            return actions;
        }

        // Switching into hunting is meant to be one deliberate act rather than a hunt through tabs,
        // so it sits where Stop would be: the thing to do when nothing is happening.
        Add(actions, StartHunting(navigator));
        Add(actions, StartUnlockRoute(navigator, "Start Unlock Route"));
        Add(actions, StartAetherCurrents());
        return actions;
    }

    /// <summary>What Wayfarer can be told to follow. One source for three surfaces: this list, the
    /// window's Following tab and the readout's own switcher cap all mean the same word.</summary>
    public IReadOnlyList<GuidanceAction> Follow()
    {
        var actions = new List<GuidanceAction>();
        if (Navigator is not { } navigator)
        {
            return actions;
        }

        // Wayfarer has no "following nothing" state: not following anything in particular IS the
        // main scenario, which is why this is a choice rather than a way to clear one. Listed
        // unconditionally and performing BOTH halves of the reset — see MainScenarioReturn for why
        // the two are independent, and why the surfaces that decided this for themselves got it
        // wrong during a hunt.
        actions.Add(new GuidanceAction(MainScenarioLabel, ReturnToMainScenario));

        Add(actions, StartUnlockRoute(navigator, null));
        Add(actions, StartHunting(navigator));
        Add(actions, StartAetherCurrents());

        // Choosing which quest needs a list of quests, which is a list and not a menu entry — so it
        // is the one choice that hands off to the window, at the tab the switcher cap reads too.
        actions.Add(new GuidanceAction("A Quest...", openFollowing));
        return actions;
    }

    /// <summary>The doors onto Wayfarer's own surfaces, and the one reset that is not a stop.</summary>
    public IReadOnlyList<GuidanceAction> Windows()
    {
        var actions = new List<GuidanceAction>();

        if (modules.Get<UnlockChecklistModule>() is { Enabled: true } unlocks)
        {
            actions.Add(new GuidanceAction("Open Unlocks", unlocks.OpenChecklist));
        }

        if (modules.Get<HuntingLogModule>() is { Enabled: true } hunting)
        {
            actions.Add(new GuidanceAction("Open Hunting Log", hunting.OpenLog));
        }

        actions.Add(new GuidanceAction("Open Settings", openSettings));

        // Nothing to reset when following the main scenario is already exactly what is happening.
        // Otherwise it is offered — including mid-hunt, which this used to exclude on the reasoning
        // that "Stop covers the engaged case". Stop does cover it, but the player asking to go back
        // to the main scenario should not have to know that the way to do it is called Stop, and the
        // condition that excluded it is the same one that greyed the switcher's own entry out.
        if (Navigator is { MainScenarioReset.Acts: true })
        {
            actions.Add(new GuidanceAction(MainScenarioLabel, ReturnToMainScenario));
        }

        return actions;
    }

    /// <summary>What the readout's plate opens, and the first entry of the menu it drops: the game's
    /// own Journal when a quest is being followed, and otherwise Wayfarer's own page for whatever IS
    /// being followed.
    ///
    /// <para><b>Never null, and that is the point.</b> This used to be <c>Journal()</c>, absent
    /// whenever what was being followed had no quest row — a hunting target, an unlock stop, an idle
    /// readout. The readout's plate called it anyway: the callback existed, so the plate grew its hit
    /// box and its controller anchor, took the press, and did nothing at all. A control that looks
    /// live and is not is worse than one that is visibly unavailable, so there is now always
    /// somewhere for the press to go.</para>
    ///
    /// <para><b>Why not the game's own Monster Note for a hunt.</b> The project prefers the game's
    /// own UI wherever the game has one, which is why a quest still goes to
    /// <see cref="QuestJournalAction"/> — but the Hunting Log's own book cannot be opened AT a
    /// target. <c>AgentMonsterNote</c> exposes <c>Show</c>/<c>Hide</c> and its own page fields, and no
    /// call that selects a rank or a creature; there is no <c>OpenWithData</c> on it. Showing it
    /// would land the player on whichever page the agent last had, which is exactly the "always lands
    /// at the top" defect <see cref="QuestJournalAction"/> documents fixing for the Journal. Our own
    /// Hunting tab names the rank, the target and its kill count, and puts the controller cursor on
    /// the button that continues the hunt — so it is the honest destination until the game's book can
    /// be opened at a row.</para></summary>
    public GuidanceAction Subject()
    {
        // The engaged mode's own page comes FIRST, ahead of the Journal. An unlock stop carries the
        // row id of a quest that has not been accepted yet, so the Journal would open and then find
        // nothing to select — the "always lands at the top" failure QuestJournalAction documents
        // fixing. The checklist has the entry, its requirements and its giver.
        var mode = Navigator?.FollowMode ?? FollowMode.MainScenario;
        if (mode == FollowMode.Hunting && modules.Get<HuntingLogModule>() is { Enabled: true } hunting)
        {
            return new GuidanceAction("Open Hunting Log", hunting.OpenLog);
        }

        if (mode == FollowMode.UnlockRoute && modules.Get<UnlockChecklistModule>() is { Enabled: true } unlocks)
        {
            return new GuidanceAction("Open Unlocks", unlocks.OpenChecklist);
        }

        if (Navigator?.Current.QuestId is { } questId and > 0)
        {
            return new GuidanceAction("Open Journal", () => QuestJournalAction.Execute(questId));
        }

        // The floor. Whatever is being followed, the tab that owns the choice can say so and can
        // change it — and its Stop button is one of the guaranteed ways back to the main scenario.
        return new GuidanceAction("Open Following", openFollowing);
    }

    private static void Add(List<GuidanceAction> actions, GuidanceAction? action)
    {
        if (action is not null)
        {
            actions.Add(action);
        }
    }

    /// <summary>The guided quest's objective is inside instanced content it can be queued for right
    /// now — see <c>DutyObjectiveGuidance</c>. The window's Quests tab has the same button.</summary>
    private static GuidanceAction? DutyFinder(NavigationState state)
    {
        if (state.DutyContentFinderConditionId is not { } cfcId)
        {
            return null;
        }

        return new GuidanceAction("Open Duty Finder", () => DutyFinderAction.Execute(cfcId));
    }

    /// <summary>Teleport routes through the existing <see cref="TeleportAction"/> gate — the
    /// click-to-teleport setting, login state and attunement — so the plugin's only server-affecting
    /// action stays exactly that, whichever menu asked for it.</summary>
    private GuidanceAction? Teleport(NavigationState state)
    {
        if (!string.Equals(state.Mode, NavigationState.Modes.OtherZone, StringComparison.Ordinal)
            || !cfg.ClickTeleportEnabled
            || !state.AetheryteUnlocked
            || state.AetheryteId is not { } aetheryteId
            || state.AetheryteName is not { } aetheryteName)
        {
            return null;
        }

        return new GuidanceAction(
            $"Teleport to {aetheryteName}", () => TeleportAction.Execute(aetheryteId, cfg, clientState, log));
    }

    /// <summary>"Start Hunting", with the RANK's remaining count so the player can see there is
    /// something to switch into. Runs the identical path the window's own button does, so both
    /// produce the same chained route through the same guidance machinery — the label and the
    /// condition are <see cref="HuntingPlan"/>'s, so neither surface can print a number the other
    /// one does not.</summary>
    private GuidanceAction? StartHunting(QuestNavigator navigator)
    {
        if (modules.Get<HuntingLogModule>() is not { Enabled: true } huntingModule)
        {
            return null;
        }

        var remaining = huntingModule.Hunting.RemainingTargets.Count;
        if (!HuntingPlan.CanStart(remaining))
        {
            return null;
        }

        return new GuidanceAction(HuntingPlan.StartLabel(remaining), navigator.StartHunt);
    }

    /// <summary>"Attune Aether Currents (4)" for the zone the player is standing in, absent
    /// everywhere else — which is most of the game: only 31 territories carry currents at all, and a
    /// zone that is already flyable has nothing to offer.
    ///
    /// <para>The count is read when the menu opens and the territory is read again when the entry is
    /// confirmed. Those can disagree if the player walks into another zone with the menu open, and
    /// the deliberate outcome is a route for where they actually are with a stale number in the label
    /// they already dismissed — the other way round would route them back to the zone they
    /// left.</para></summary>
    private GuidanceAction? StartAetherCurrents()
    {
        if (modules.Get<AetherCurrentsModule>() is not { Enabled: true } module)
        {
            return null;
        }

        var remaining = module.Currents.RemainingIn(clientState.TerritoryType);
        if (remaining.Count == 0)
        {
            return null;
        }

        return new GuidanceAction(
            $"Attune Aether Currents ({remaining.Count})",
            () => module.StartRoute(clientState.TerritoryType));
    }

    /// <summary>A route through every available, locatable unlock — the same predicate and ordering
    /// (<see cref="RoutePlanner.Order"/>) as the unlocks window's "Route me" button. Named for where
    /// it is offered: the follow list says what it would follow and how much of it there is, the
    /// action list says what it would start.</summary>
    private GuidanceAction? StartUnlockRoute(QuestNavigator navigator, string? label)
    {
        if (modules.Get<UnlockChecklistModule>() is not { Enabled: true } unlockModule)
        {
            return null;
        }

        var routable = unlockModule.Unlocks.Entries
            .Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null)
            .ToList();
        if (routable.Count == 0)
        {
            return null;
        }

        return new GuidanceAction(
            label ?? $"Unlock Route ({routable.Count})",
            () => StartRoute(navigator, unlockModule, routable));
    }

    /// <summary>Both halves of the reset, always, in this order: release whatever is engaged, then
    /// drop the followed quest. Either one alone leaves the player somewhere they did not ask to
    /// be.</summary>
    private void ReturnToMainScenario()
    {
        if (Navigator is not { } navigator)
        {
            return;
        }

        navigator.ClearPickup();
        navigator.FollowedOverride = null;
    }

    private void StartRoute(
        QuestNavigator navigator, UnlockChecklistModule unlockModule, List<ResolvedUnlock> routable)
    {
        var player = objects.LocalPlayer;
        var ordered = RoutePlanner.Order(
            routable, clientState.TerritoryType, player?.Position.X ?? 0, player?.Position.Z ?? 0);
        var targets = ordered.Select(unlockModule.Unlocks.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
        if (targets.Count > 0)
        {
            navigator.SetRoute(targets);
        }
    }
}
