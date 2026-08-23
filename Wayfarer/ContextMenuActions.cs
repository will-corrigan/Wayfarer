using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer;

/// <summary>Injects a "Wayfarer" submenu into the game's own Default context menu — a native row
/// that inherits the game's own d-pad focus navigation, no cursor required.
///
/// This is the controller's action surface. A controller gets the click-through readout, which by
/// construction carries no affordances — not even the settings cog a mouse gets, because a cog on a
/// surface that cannot be clicked would be a lie — so this is where starting a hunt, stopping one,
/// reaching the unlocks list, opening settings and taking the teleport the readout is recommending
/// all live without a cursor and without typing a command. A mouse clicks the readout itself, which
/// is why this is off for a mouse by default. <see cref="QuestHelperConfig.MenuMode"/> therefore
/// defaults to <see cref="ContextMenuMode.ControllerOnly"/> — evaluated fresh via
/// <see cref="InputModeService.Mode"/> on every
/// menu open (not registered/unregistered on mode flips, since checking is cheap and avoids a
/// second subscription to manage). Either way, only <see cref="ContextMenuType.Default"/> is
/// ever handled — the game's own Inventory-type menu is never touched. The outer "Wayfarer" item
/// is added from <see cref="IContextMenu.OnMenuOpened"/>; its children are built lazily from
/// <see cref="IMenuItem.OnClicked"/> (via <see
/// cref="IMenuItemClickedArgs.OpenSubmenu(Dalamud.Game.Text.SeStringHandling.SeString,
/// IReadOnlyList{IMenuItem})"/>) rather than at menu-open time, so a slow player never sees a
/// submenu built from state that's gone stale by the time they confirm it.
///
/// Every entry is hidden rather than disabled when inapplicable: no active teleport suggestion, no
/// routable unlock, nothing to cancel/follow back from. The whole submenu is absent when Quest
/// Helper is disabled — every entry ultimately reads or drives its <see cref="QuestNavigator"/>.
/// The unlock-route entries are additionally gated on Unlock Checklist being enabled. Teleport
/// routes through the existing <see cref="TeleportAction"/> gate (click-to-teleport setting,
/// login state, attunement) — the plugin's only server-affecting action stays exactly that.
///
/// Pure DI: constructed once by <see cref="Plugin"/>, and disposed by unregistering the one event
/// subscription.</summary>
internal sealed class ContextMenuActions : IDisposable
{
    private readonly IContextMenu contextMenu;
    private readonly IObjectTable objects;
    private readonly ModuleRegistry modules;
    private readonly QuestHelperConfig cfg;
    private readonly IClientState clientState;
    private readonly InputModeService inputMode;
    private readonly Action openSettings;
    private readonly IPluginLog log;

    private bool loggedMenuFailure;

    public ContextMenuActions(
        IContextMenu contextMenu,
        IObjectTable objects,
        ModuleRegistry modules,
        QuestHelperConfig cfg,
        IClientState clientState,
        InputModeService inputMode,
        Action openSettings,
        IPluginLog log)
    {
        this.contextMenu = contextMenu;
        this.objects = objects;
        this.modules = modules;
        this.cfg = cfg;
        this.clientState = clientState;
        this.inputMode = inputMode;
        this.openSettings = openSettings;
        this.log = log;
        contextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose() => contextMenu.OnMenuOpened -= OnMenuOpened;

    /// <summary>Runs inside the game's own menu-building path, which is why it cannot be allowed to
    /// throw: an exception here surfaces as the player's right-click menu failing, not as a Wayfarer
    /// problem, and this is a controller player's main way in. Every other game callback on this
    /// plugin is wrapped; this one was the exception.</summary>
    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        try
        {
            if (args.MenuType != ContextMenuType.Default
                || modules.Get<QuestHelperModule>() is not { Enabled: true }
                || !ShouldShowMenu())
            {
                return;
            }

            args.AddMenuItem(new MenuItem
            {
                Name = "Wayfarer",
                IsSubmenu = true,
                OnClicked = OnWayfarerClicked,
            });
        }
        catch (Exception ex)
        {
            // Once: this runs on every right-click, so a repeatable fault here is a line per menu.
            if (!loggedMenuFailure)
            {
                loggedMenuFailure = true;
                const string message =
                    "Wayfarer: the Wayfarer entry could not be added to the game's right-click menu, so it "
                    + "will be missing there. The game's own menu is unaffected and every other way into "
                    + "Wayfarer keeps working. Reported once.";
                log.Warning(ex, message);
            }
        }
    }

    /// <summary>Evaluated fresh on every menu open (see class doc comment) — cheap enough that a
    /// second event subscription to track input-mode flips isn't worth the complexity.</summary>
    private bool ShouldShowMenu() => cfg.MenuMode switch
    {
        ContextMenuMode.Always => true,
        ContextMenuMode.ControllerOnly => inputMode.Mode == InputMode.Controller,
        _ => false,
    };

    private void OnWayfarerClicked(IMenuItemClickedArgs args) =>
        args.OpenSubmenu("Wayfarer", BuildSubmenuItems());

    /// <summary>Rebuilt from live state every time the submenu opens (Quest Helper is re-checked
    /// too — it could have been disabled from the config window between the outer click and this
    /// one).</summary>
    private List<IMenuItem> BuildSubmenuItems()
    {
        var items = new List<IMenuItem>();
        if (modules.Get<QuestHelperModule>() is not { Enabled: true } questHelper)
        {
            return items;
        }

        var navigator = questHelper.Navigator;
        var state = navigator.Current;

        AddTeleportItem(items, state);

        // The universal exit, shown whenever anything is engaged — a chained unlock route, a
        // single unlock pickup or a hunt alike, since ClearPickup() (guidance.Arbiter.ReleaseAll())
        // is the one release valve for all three. Offered here rather than only as a route-specific
        // "Cancel route" (the previous behaviour) because a single, unchained pickup or an active
        // hunt is just as much "something the player asked for" that needs a way out, and this is
        // the only entry point a controller has by default.
        if (state.Engaged)
        {
            items.Add(new MenuItem { Name = "Stop", OnClicked = _ => navigator.ClearPickup() });
        }
        else
        {
            // Switching into hunting is meant to be one deliberate act, not a hunt through tabs —
            // so it sits here, beside Stop, which is the one act that ends it. The same pair exists
            // on the window's Hunting Log tab; this is the version that needs no cursor.
            AddStartHuntItem(items, navigator);

            if (modules.Get<UnlockChecklistModule>() is { Enabled: true } routableModule)
            {
                AddStartRouteItem(items, navigator, routableModule);
            }
        }

        AddWindowItems(items, navigator, state);
        return items;
    }

    private void AddWindowItems(List<IMenuItem> items, QuestNavigator navigator, NavigationState state)
    {
        if (modules.Get<UnlockChecklistModule>() is { Enabled: true } unlockModule)
        {
            items.Add(new MenuItem
            {
                Name = "Open unlocks",
                OnClicked = _ => unlockModule.OpenChecklist(),
            });
        }

        if (modules.Get<HuntingLogModule>() is { Enabled: true } huntingModule)
        {
            items.Add(new MenuItem
            {
                Name = "Open hunting log",
                OnClicked = _ => huntingModule.OpenLog(),
            });
        }

        // The controller's answer to the readout's settings cog. A mouse clicks the cog on the
        // readout itself; a controller cannot, because its readout is the click-through overlay —
        // so the same one press lands here instead of behind a walk through the plugin list.
        items.Add(new MenuItem
        {
            Name = "Open settings",
            OnClicked = _ => openSettings(),
        });

        // Nothing to reset when nothing is engaged and no override is set — following the MSQ is
        // already exactly what's happening. The "Stop" item above already covers the engaged case.
        if (!state.Engaged && navigator.FollowedOverride is not null)
        {
            items.Add(new MenuItem
            {
                Name = "Follow MSQ",
                OnClicked = _ => navigator.FollowedOverride = null,
            });
        }
    }

    private void AddTeleportItem(List<IMenuItem> items, NavigationState state)
    {
        if (string.Equals(state.Mode, NavigationState.Modes.OtherZone, StringComparison.Ordinal)
            && cfg.ClickTeleportEnabled
            && state.AetheryteUnlocked
            && state.AetheryteId is { } aetheryteId
            && state.AetheryteName is { } aetheryteName)
        {
            items.Add(new MenuItem
            {
                Name = $"Teleport to {aetheryteName}",
                OnClicked = _ => TeleportAction.Execute(aetheryteId, cfg, clientState, log),
            });
        }

        // The guided quest's objective is inside instanced content it can be queued for right now
        // (see DutyObjectiveGuidance). This is the cursor-free way to reach it; the window's Quests
        // tab has the same button for a mouse.
        if (state.DutyContentFinderConditionId is { } cfcId)
        {
            items.Add(new MenuItem
            {
                Name = "Open Duty Finder",
                OnClicked = _ => DutyFinderAction.Execute(cfcId),
            });
        }
    }

    /// <summary>"Start hunting" — the deliberate switch into hunting mode, with the rank's remaining
    /// count so the player can see there is something to switch into. Runs the identical path the
    /// window's own "Start hunting" button does, so both produce the same chained route through the
    /// same guidance machinery.</summary>
    private void AddStartHuntItem(List<IMenuItem> items, QuestNavigator navigator)
    {
        if (modules.Get<HuntingLogModule>() is not { Enabled: true } huntingModule)
        {
            return;
        }

        var order = huntingModule.Hunting.HuntHereOrder;
        if (order.Count == 0)
        {
            return;
        }

        items.Add(new MenuItem
        {
            Name = $"Start hunting ({order.Count})",
            OnClicked = _ =>
            {
                var targets = order.Select(huntingModule.Hunting.ToPickupTarget)
                                   .Where(t => t != null)
                                   .Select(t => t!)
                                   .ToList();
                if (targets.Count > 0)
                {
                    navigator.SetRoute(targets);
                }
            },
        });
    }

    /// <summary>"Start unlock route" when at least one available, locatable unlock exists to route
    /// through — the same predicate and ordering (<see cref="RoutePlanner.Order"/>) as
    /// UnlockWindow's "Route me" button. Only ever offered while nothing is already engaged (see
    /// the caller) — the "Stop" item is what ends a route once one is running.</summary>
    private void AddStartRouteItem(List<IMenuItem> items, QuestNavigator navigator, UnlockChecklistModule unlockModule)
    {
        var routable = unlockModule.Unlocks.Entries
            .Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null)
            .ToList();
        if (routable.Count == 0)
        {
            return;
        }

        items.Add(new MenuItem
        {
            Name = "Start unlock route",
            OnClicked = _ => StartUnlockRoute(navigator, unlockModule, routable),
        });
    }

    private void StartUnlockRoute(
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
