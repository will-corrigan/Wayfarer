using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer;

/// <summary>Injects a "Wayfarer" submenu into the game's own Default context menu — a native row
/// that inherits the game's own d-pad focus navigation, no cursor required (spec §2). By default
/// (<see cref="QuestHelperConfig.MenuEverywhere"/> = true) it appears on ANY Default-type menu:
/// any NPC/nameplate right-click, the local player's own nameplate/portrait/party-list row, or a
/// controller subcommand menu. The original design gated this to a self-target menu only, but
/// that turned out unusable on a real HUD — no solo party frame to right-click, the self-model
/// right-click is finicky, and F1-self-targeting was rejected as a workaround — so
/// self-target-only is now an opt-IN restriction (<c>MenuEverywhere = false</c>) rather than the
/// default. Either way, the game's own Inventory-type context menu is never touched — only
/// <see cref="ContextMenuType.Default"/> is handled at all. The outer "Wayfarer" item is added
/// from <see cref="IContextMenu.OnMenuOpened"/>; its children are built lazily from
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
/// Pure DI: constructed once by <see cref="Plugin"/> with the same services already threaded
/// through <see cref="Windows.ArrowWindow"/>/<see cref="Windows.UnlockWindow"/>, and disposed by
/// unregistering the one event subscription.</summary>
internal sealed class ContextMenuActions : IDisposable
{
    private readonly IContextMenu contextMenu;
    private readonly IObjectTable objects;
    private readonly ModuleRegistry modules;
    private readonly QuestHelperConfig cfg;
    private readonly IClientState clientState;
    private readonly IPluginLog log;

    public ContextMenuActions(
        IContextMenu contextMenu,
        IObjectTable objects,
        ModuleRegistry modules,
        QuestHelperConfig cfg,
        IClientState clientState,
        IPluginLog log)
    {
        this.contextMenu = contextMenu;
        this.objects = objects;
        this.modules = modules;
        this.cfg = cfg;
        this.clientState = clientState;
        this.log = log;
        contextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose() => contextMenu.OnMenuOpened -= OnMenuOpened;

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.MenuType != ContextMenuType.Default
            || modules.Get<QuestHelperModule>() is not { Enabled: true }
            || (!cfg.MenuEverywhere && !IsSelfTarget(args.Target)))
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

    /// <summary>True only for a Default-menu target that names the local player. Only consulted
    /// when <see cref="QuestHelperConfig.MenuEverywhere"/> is false — the opt-in restriction back
    /// to the original self-target-only gating (own nameplate/portrait/party-list row); every
    /// other MenuTarget shape (party/friend list entries for OTHER characters, no target at all)
    /// resolves false and the outer item is simply never added.</summary>
    private bool IsSelfTarget(MenuTarget target) =>
        target is MenuTargetDefault def
        && objects.LocalPlayer is { } player
        && def.TargetObjectId == player.GameObjectId;

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

        if (modules.Get<UnlockChecklistModule>() is { Enabled: true } unlockModule)
        {
            AddUnlockRouteItem(items, navigator, unlockModule, state);
            items.Add(new MenuItem
            {
                Name = "Open checklist",
                OnClicked = _ => unlockModule.Window.IsOpen = true,
            });
        }

        // Nothing to reset when neither an override nor a pickup/route is active — following the
        // MSQ is already exactly what's happening.
        if (navigator.FollowedOverride is not null || navigator.Pickup is not null)
        {
            items.Add(new MenuItem
            {
                Name = "Follow MSQ",
                OnClicked = _ =>
                {
                    navigator.ClearPickup();
                    navigator.FollowedOverride = null;
                },
            });
        }

        return items;
    }

    /// <summary>"Cancel route" while a multi-stop route is active (same RouteTotal-not-null check
    /// as the ArrowWindow quest picker's popup), otherwise "Start unlock route" when at least one
    /// available, locatable unlock exists to route through — the same predicate and ordering
    /// (<see cref="RoutePlanner.Order"/>) as UnlockWindow's "Route me" button.</summary>
    private void AddUnlockRouteItem(
        List<IMenuItem> items, QuestNavigator navigator, UnlockChecklistModule unlockModule, NavigationState state)
    {
        if (state.RouteTotal is not null)
        {
            items.Add(new MenuItem { Name = "Cancel route", OnClicked = _ => navigator.ClearPickup() });
            return;
        }

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
