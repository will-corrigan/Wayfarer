using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Guidance.Sources;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Tracks every quest-unlockable feature, mount and dungeon the player can pick up
/// right now, and lets the checklist route <see cref="QuestHelperModule"/>'s arrow to the
/// quest givers (task-5-brief.md delta 3).</summary>
internal sealed class UnlockChecklistModule(
    IFramework framework,
    WindowSystem windows,
    UnlockService unlocks,
    UnlockWindow unlockWindow,
    NativeHubWindow hub,
    UnlockChecklistConfig cfg,
    IPluginLog log,
    IGuidanceArbiter arbiter,
    UnlockRouteSource routeSource) : IModule
{
    public string Name => "Unlock Checklist";

    public string Description => "Tracks every quest-unlockable feature, mount and dungeon you can pick up right now, and routes you to the quest givers.";

    public bool Enabled { get; private set; }

    internal UnlockService Unlocks { get; } = unlocks;

    internal UnlockWindow Window { get; } = unlockWindow;

    /// <summary>Read by <see cref="Windows.ArrowWindow"/> for the glanceable-lines toggle
    /// (spec §4, task A3) — the coherent home for it since the data comes from this module.</summary>
    internal UnlockChecklistConfig Config { get; } = cfg;

    /// <summary>Opens the checklist. There is one checklist and it is the native window, for mouse
    /// and controller alike — the game's own windows are clickable and cursor-navigable at the same
    /// time, so a second ImGui copy would only be a second thing to keep in step. That copy now
    /// exists solely as an automatic fallback: if the native window cannot be created, this logs
    /// once and opens the old one rather than leaving the player with nothing.</summary>
    public void OpenChecklist()
    {
        try
        {
            Window.IsOpen = false;
            hub.OpenTab(HubTab.Checklist);
            return;
        }
        catch (Exception ex)
        {
            log.Error(ex, "UnlockChecklistModule: the native window failed to open — falling back to the ImGui checklist.");
        }

        CloseNativeWindow();
        Window.IsOpen = true;
    }

    public void Enable()
    {
        Enabled = true;
        framework.Update += Unlocks.OnFrameworkUpdate;

        // Its own source, not another module's navigator: this used to reach into QuestHelperModule
        // by concrete type to hear about pickups advancing, which is the coupling that makes every
        // new module cost an edit to an existing one.
        arbiter.Register(routeSource);
        routeSource.OnAdvanced += Unlocks.OnPickupAdvanced;
        windows.AddWindow(Window);
    }

    public void Disable()
    {
        Enabled = false;
        windows.RemoveWindow(Window);
        CloseNativeWindow();
        routeSource.OnAdvanced -= Unlocks.OnPickupAdvanced;

        // Unregistering releases the engagement token if this module's route currently owns the
        // arrow, so a disabled module can never keep guiding.
        arbiter.Unregister(routeSource);
        framework.Update -= Unlocks.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (Enabled)
        {
            Disable();
        }

        // The hub is shared with HuntingLogModule and owned/disposed by Plugin directly, not by
        // either module — see NativeHubWindow's doc comment.
    }

    /// <summary>Closes the hub if it is open — used by <see cref="Disable"/> (an open hub would
    /// otherwise linger on screen polling frozen service state; the ImGui counterpart disappears
    /// with its WindowSystem removal) and by <see cref="OpenChecklist"/>'s ImGui path to keep the
    /// two presentations mutually exclusive.</summary>
    private void CloseNativeWindow()
    {
        if (hub.IsOpen)
        {
            hub.Close();
        }
    }
}
