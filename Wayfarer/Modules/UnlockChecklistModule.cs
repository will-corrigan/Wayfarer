using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Input;
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
    InputModeService inputMode,
    UnlockChecklistConfig cfg,
    Action saveConfig,
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

    /// <summary>Opens whichever checklist presentation matches the player's current
    /// <see cref="InputModeService.Mode"/> (spec §3): the shared native hub window (Checklist tab)
    /// on Controller, the unchanged ImGui window on Mouse. A hub open failure falls back to the
    /// ImGui window with a single log line rather than leaving the player with nothing.</summary>
    public void OpenChecklist()
    {
        if (inputMode.Mode == InputMode.Controller)
        {
            try
            {
                // Close the other presentation first so an input-mode flip between opens can't
                // leave both windows on screen at once.
                Window.IsOpen = false;
                hub.OpenTab(HubTab.Checklist);
                return;
            }
            catch (Exception ex)
            {
                log.Error(ex, "UnlockChecklistModule: native hub failed to open — falling back to the ImGui checklist.");
            }
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

    public void DrawConfig()
    {
        if (ImGui.Button("Open checklist"))
        {
            OpenChecklist();
        }

        var showOnWidget = Config.ShowOnWidget;
        if (ImGui.Checkbox("Show top unlocks on the quest widget", ref showOnWidget))
        {
            Config.ShowOnWidget = showOnWidget;
            saveConfig();
        }
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
