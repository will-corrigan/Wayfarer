using Dalamud.Plugin.Services;
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
/// where a choice with nothing behind it is listed and disabled instead, because a choice that
/// vanishes cannot be learned — that rule lives with the follow choices themselves.</para>
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
        // main scenario, which is why this is a choice rather than a way to clear one.
        actions.Add(new GuidanceAction("Main Scenario", () =>
        {
            navigator.ClearPickup();
            navigator.FollowedOverride = null;
        }));

        Add(actions, StartUnlockRoute(navigator, null));
        Add(actions, StartHunting(navigator));

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

        // Nothing to reset when nothing is engaged and no override is set — following the main
        // scenario is already exactly what is happening, and Stop covers the engaged case.
        if (Navigator is { } navigator && !navigator.Current.Engaged && navigator.FollowedOverride is not null)
        {
            actions.Add(new GuidanceAction("Main Scenario", () => navigator.FollowedOverride = null));
        }

        return actions;
    }

    /// <summary>The game's own Journal, at whatever is being followed — the readout's plate does this
    /// on a click, and this is the same action as an entry for the menu a controller gets. Absent
    /// when what is being followed is not a quest, which is when there is no Journal page to open.
    /// </summary>
    public GuidanceAction? Journal()
    {
        if (Navigator?.Current.QuestId is not { } questId)
        {
            return null;
        }

        return new GuidanceAction("Open Journal", () => QuestJournalAction.Execute(questId));
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

    /// <summary>"Start Hunting", with the rank's remaining count so the player can see there is
    /// something to switch into. Runs the identical path the window's own button does, so both
    /// produce the same chained route through the same guidance machinery.</summary>
    private GuidanceAction? StartHunting(QuestNavigator navigator)
    {
        if (modules.Get<HuntingLogModule>() is not { Enabled: true } huntingModule)
        {
            return null;
        }

        var order = huntingModule.Hunting.HuntHereOrder;
        if (order.Count == 0)
        {
            return null;
        }

        return new GuidanceAction($"Start Hunting ({order.Count})", () =>
        {
            var targets = order.Select(huntingModule.Hunting.ToPickupTarget)
                               .Where(t => t != null)
                               .Select(t => t!)
                               .ToList();
            if (targets.Count > 0)
            {
                navigator.SetRoute(targets);
            }
        });
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
