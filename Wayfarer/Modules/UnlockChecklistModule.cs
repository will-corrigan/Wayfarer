using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Tracks every quest-unlockable feature, mount and dungeon the player can pick up
/// right now, and lets the checklist route <see cref="QuestHelperModule"/>'s arrow to the
/// quest givers (task-5-brief.md delta 3).</summary>
internal sealed class UnlockChecklistModule(
    IFramework framework,
    WindowSystem windows,
    ModuleRegistry modules,
    UnlockService unlocks,
    UnlockWindow unlockWindow,
    NativeUnlockWindow nativeUnlockWindow,
    InputModeService inputMode,
    UnlockChecklistConfig cfg,
    Action saveConfig,
    IPluginLog log) : IModule
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
    /// <see cref="InputModeService.Mode"/> (spec §3): the native KamiToolKit window on
    /// Controller, the unchanged ImGui window on Mouse. A native-window open failure falls back
    /// to the ImGui window with a single log line rather than leaving the player with nothing.</summary>
    public void OpenChecklist()
    {
        if (inputMode.Mode == InputMode.Controller)
        {
            try
            {
                // Close the other presentation first so an input-mode flip between opens can't
                // leave both windows on screen at once.
                Window.IsOpen = false;
                nativeUnlockWindow.Open();
                return;
            }
            catch (Exception ex)
            {
                log.Error(ex, "UnlockChecklistModule: native checklist window failed to open — falling back to the ImGui checklist.");
            }
        }

        CloseNativeWindow();
        Window.IsOpen = true;
    }

    public void Enable()
    {
        Enabled = true;
        framework.Update += Unlocks.OnFrameworkUpdate;
        if (modules.Get<QuestHelperModule>() is { } questHelper)
        {
            questHelper.Navigator.OnPickupAdvanced += Unlocks.OnPickupAdvanced;
        }

        windows.AddWindow(Window);
    }

    public void Disable()
    {
        Enabled = false;
        windows.RemoveWindow(Window);
        CloseNativeWindow();
        if (modules.Get<QuestHelperModule>() is { } questHelper)
        {
            questHelper.Navigator.OnPickupAdvanced -= Unlocks.OnPickupAdvanced;
        }

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

        nativeUnlockWindow.Dispose();
    }

    /// <summary>Closes the native window if it is open — used by <see cref="Disable"/> (an open
    /// native window would otherwise linger on screen polling frozen service state; the ImGui
    /// counterpart disappears with its WindowSystem removal) and by <see cref="OpenChecklist"/>'s
    /// ImGui path to keep the two presentations mutually exclusive.</summary>
    private void CloseNativeWindow()
    {
        if (nativeUnlockWindow.IsOpen)
        {
            nativeUnlockWindow.Close();
        }
    }
}
