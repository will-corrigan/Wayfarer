using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Guidance.Sources;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Reads live hunting-log progress, resolves the current page's
/// remaining targets, and routes the arrow to them via the same pickup-target machinery
/// <see cref="UnlockChecklistModule"/> uses. Registered <c>enabledByDefault: true</c> — same
/// default as <see cref="UnlockChecklistModule"/> and <see cref="QuestHelperModule"/>, the only two
/// other modules this registry hosts.</summary>
internal sealed class HuntingLogModule(
    IFramework framework,
    WindowSystem windows,
    HuntingLogService hunting,
    HuntingWindow huntingWindow,
    NativeHubWindow hub,
    HuntingLogConfig cfg,
    IPluginLog log,
    IGuidanceArbiter arbiter,
    HuntingSource huntingSource) : IModule
{
    private bool loggedNativeFallback;

    public string Name => "Hunting Log";

    public string Description => "Tracks your hunting log and routes you to what is left.";

    public bool Enabled { get; private set; }

    internal HuntingLogService Hunting { get; } = hunting;

    internal HuntingWindow Window { get; } = huntingWindow;

    /// <summary>Read by <see cref="Windows.ReadoutFeed"/> for the hunting-progress toggle (spec
    /// §4/§5) — the coherent home for it since the data comes from this module, mirroring
    /// <see cref="UnlockChecklistModule.Config"/>.</summary>
    internal HuntingLogConfig Config { get; } = cfg;

    /// <summary>Opens the hunting log — the native window, for mouse and controller alike. The
    /// ImGui copy survives only as the automatic fallback for a native window that cannot be
    /// created; same shape as <see cref="UnlockChecklistModule.OpenChecklist"/>.</summary>
    public void OpenLog()
    {
        try
        {
            Window.IsOpen = false;
            hub.OpenTab(HubTab.Hunting);
            return;
        }
        catch (Exception ex)
        {
            // Once, for the same reason as UnlockChecklistModule.OpenChecklist.
            if (!loggedNativeFallback)
            {
                loggedNativeFallback = true;
                const string message =
                    "Wayfarer: the game-styled hunting window would not open, so the plugin-drawn one is being "
                    + "used instead. Hunting still works; the window is best driven with a mouse. Reported once.";
                log.Warning(ex, message);
            }
        }

        CloseNativeWindow();
        Window.IsOpen = true;
    }

    public void Enable()
    {
        Enabled = true;
        framework.Update += Hunting.OnFrameworkUpdate;
        arbiter.Register(huntingSource);
        windows.AddWindow(Window);
    }

    public void Disable()
    {
        Enabled = false;
        windows.RemoveWindow(Window);

        // Vacates the Hunting tab if it happens to be the one on screen; never closes the hub
        // outright. The only reachable caller of Disable() is the Settings tab's own checkbox, so
        // in practice the player is always on Settings — not Hunting — when this runs, and the hub
        // must stay exactly as they left it rather than closing under their next click.
        hub.LeaveTabIfActive(HubTab.Hunting);

        // Unregistering releases the engagement token if a hunt currently owns the arrow, so a
        // disabled module can never keep guiding.
        arbiter.Unregister(huntingSource);
        framework.Update -= Hunting.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (Enabled)
        {
            Disable();
        }

        // The hub is shared with UnlockChecklistModule and owned/disposed by Plugin directly, not
        // by either module — see NativeHubWindow's doc comment.
    }

    /// <summary>Closes the hub if it is open — used by <see cref="OpenLog"/>'s ImGui fallback path
    /// to keep the two presentations mutually exclusive. Not used by <see cref="Disable"/>:
    /// disabling a module must not close a multi-tab window it does not own outright, only vacate
    /// its own tab if that happens to be the one showing — see
    /// <see cref="NativeHubWindow.LeaveTabIfActive"/>.</summary>
    private void CloseNativeWindow()
    {
        if (hub.IsOpen)
        {
            hub.Close();
        }
    }
}
