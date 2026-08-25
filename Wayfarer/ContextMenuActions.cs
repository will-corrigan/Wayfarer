using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;
using Wayfarer.Modules;

namespace Wayfarer;

/// <summary>Injects a "Wayfarer" submenu into the game's own Default context menu — a native row
/// that inherits the game's own d-pad focus navigation, no cursor required.
///
/// This is one of two menus onto the same actions. The other is the one the readout's plate drops
/// when it is asked for subcommands (<see cref="Windows.Native.ReadoutMenu"/>); both render
/// <see cref="GuidanceActions"/>, so neither can offer something the other does not. This one is
/// still the only route while the readout is hidden or has nothing to say, and it is the only one
/// that works with no readout on screen at all — which is why it stays exactly as it was.
///
/// A mouse operates the readout directly, which is why this is off for a mouse by default.
/// <see cref="QuestHelperConfig.MenuMode"/> defaults to
/// <see cref="ContextMenuMode.ControllerOnly"/> — evaluated fresh via
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
/// Every entry is hidden rather than disabled when inapplicable, and the whole submenu is absent
/// when Quest Helper is disabled — both decided in <see cref="GuidanceActions"/>, which is also
/// where the teleport's own gate lives.
///
/// Pure DI: constructed once by <see cref="Plugin"/>, and disposed by unregistering the one event
/// subscription.</summary>
internal sealed class ContextMenuActions : IDisposable
{
    private readonly IContextMenu contextMenu;
    private readonly ModuleRegistry modules;
    private readonly QuestHelperConfig cfg;
    private readonly GuidanceActions actions;
    private readonly InputModeService inputMode;
    private readonly IPluginLog log;

    private bool loggedMenuFailure;

    public ContextMenuActions(
        IContextMenu contextMenu,
        ModuleRegistry modules,
        QuestHelperConfig cfg,
        GuidanceActions actions,
        InputModeService inputMode,
        IPluginLog log)
    {
        this.contextMenu = contextMenu;
        this.modules = modules;
        this.cfg = cfg;
        this.actions = actions;
        this.inputMode = inputMode;
        this.log = log;
        contextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose() => contextMenu.OnMenuOpened -= OnMenuOpened;

    private static IMenuItem Item(GuidanceAction action) =>
        new MenuItem { Name = action.Label, OnClicked = _ => action.Invoke() };

    /// <summary>Runs inside the game's own menu-building path, which is why it cannot be allowed to
    /// throw: an exception here surfaces as the player's right-click menu failing, not as a Wayfarer
    /// problem. Every other game callback on this plugin is wrapped; this one was the exception.</summary>
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

    /// <summary>Rebuilt from live state every time the submenu opens — the same order this menu has
    /// always had: what to do now, then what to follow, then the doors onto Wayfarer's own
    /// windows.</summary>
    private List<IMenuItem> BuildSubmenuItems()
    {
        var items = new List<IMenuItem>();

        foreach (var action in actions.Route())
        {
            items.Add(Item(action));
        }

        // A real submenu rather than a flat list: it inherits the game's own d-pad navigation, which
        // is the whole reason this menu is an action surface for a controller at all.
        items.Add(new MenuItem
        {
            Name = "Follow",
            IsSubmenu = true,
            OnClicked = args => args.OpenSubmenu("Follow", BuildFollowItems()),
        });

        foreach (var action in actions.Windows())
        {
            items.Add(Item(action));
        }

        return items;
    }

    /// <summary>Built when the submenu opens rather than when the menu does, so a slow player never
    /// confirms a choice built from state that has since gone stale.</summary>
    private List<IMenuItem> BuildFollowItems() => [.. actions.Follow().Select(Item)];
}
