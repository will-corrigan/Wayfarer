using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Third module (spec §5): reads live hunting-log progress, resolves the current page's
/// remaining targets, and routes the arrow to them via the same pickup-target machinery
/// <see cref="UnlockChecklistModule"/> uses. Registered <c>enabledByDefault: true</c> — same
/// default as <see cref="UnlockChecklistModule"/> and <see cref="QuestHelperModule"/>, the only two
/// other modules this registry hosts.</summary>
internal sealed class HuntingLogModule(
    IFramework framework,
    WindowSystem windows,
    HuntingLogService hunting,
    HuntingWindow huntingWindow,
    NativeHuntingWindow nativeHuntingWindow,
    InputModeService inputMode,
    HuntingLogConfig cfg,
    Action saveConfig,
    IPluginLog log) : IModule
{
    public string Name => "Hunting Log";

    public string Description => "Tracks your current class/job hunting log (or the Grand Company Elite logs once unlocked) and routes you to the remaining targets.";

    public bool Enabled { get; private set; }

    internal HuntingLogService Hunting { get; } = hunting;

    internal HuntingWindow Window { get; } = huntingWindow;

    /// <summary>Read by <see cref="Windows.ArrowWindow"/> for the glanceable-line toggle (spec
    /// §4/§5) — the coherent home for it since the data comes from this module, mirroring
    /// <see cref="UnlockChecklistModule.Config"/>.</summary>
    internal HuntingLogConfig Config { get; } = cfg;

    /// <summary>Opens whichever hunting-log presentation matches the player's current
    /// <see cref="InputModeService.Mode"/> (spec §3/§5): the native KamiToolKit window on
    /// Controller, the ImGui window on Mouse. A native-window open failure falls back to the ImGui
    /// window with a single log line — same fallback shape as <see cref="UnlockChecklistModule.OpenChecklist"/>.</summary>
    public void OpenLog()
    {
        if (inputMode.Mode == InputMode.Controller)
        {
            try
            {
                nativeHuntingWindow.Open();
                return;
            }
            catch (Exception ex)
            {
                log.Error(ex, "HuntingLogModule: native hunting log window failed to open — falling back to the ImGui window.");
            }
        }

        Window.IsOpen = true;
    }

    public void Enable()
    {
        Enabled = true;
        framework.Update += Hunting.OnFrameworkUpdate;
        windows.AddWindow(Window);
    }

    public void Disable()
    {
        Enabled = false;
        windows.RemoveWindow(Window);
        framework.Update -= Hunting.OnFrameworkUpdate;
    }

    public void DrawConfig()
    {
        if (ImGui.Button("Open hunting log"))
        {
            OpenLog();
        }

        var showOnWidget = Config.ShowOnWidget;
        if (ImGui.Checkbox("Show current target on the quest widget", ref showOnWidget))
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

        nativeHuntingWindow.Dispose();
    }
}
