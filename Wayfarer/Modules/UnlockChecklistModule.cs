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
    /// <summary>What the feature is called on screen — the label of its switch in Settings, and the
    /// name of its tab. "Unlocks" rather than the old "Unlock Checklist": the player could not tell
    /// what a checklist was ("Is checklist the unlocks section?"). Public because
    /// <see cref="Configuration"/> has to migrate the saved enabled flag off the old key, and that
    /// key was this string.</summary>
    public const string FeatureName = "Unlocks";

    private bool loggedNativeFallback;

    public string Name => FeatureName;

    public string Description => "Tracks what you can unlock now, and routes you to the quest givers.";

    public bool Enabled { get; private set; }

    internal UnlockService Unlocks { get; } = unlocks;

    internal UnlockWindow Window { get; } = unlockWindow;

    /// <summary>Read by <see cref="Windows.ReadoutFeed"/> for the "unlocks nearby" toggle — the
    /// coherent home for it since the data comes from this module.</summary>
    internal UnlockChecklistConfig Config { get; } = cfg;

    /// <summary>Opens the unlocks list. There is one and it is the native window, for mouse
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
            // Once: the player can open the unlocks list as often as they like, and the reason it
            // would not open the first time is the reason it will not open the tenth.
            if (!loggedNativeFallback)
            {
                loggedNativeFallback = true;
                const string message =
                    "Wayfarer: the game-styled unlocks window would not open, so the plugin-drawn one "
                    + "is being used instead. Everything is still reachable; it will not look like the game's "
                    + "own windows and is best driven with a mouse. Reported once.";
                log.Warning(ex, message);
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

        // Vacates the Checklist tab if it happens to be the one on screen; never closes the hub
        // outright. The only reachable caller of Disable() is the Settings tab's own checkbox, so
        // in practice the player is always on Settings — not Checklist — when this runs, and the
        // hub must stay exactly as they left it rather than closing under their next click.
        hub.LeaveTabIfActive(HubTab.Checklist);
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

    /// <summary>Closes the hub if it is open — used by <see cref="OpenChecklist"/>'s ImGui fallback
    /// path to keep the two presentations mutually exclusive. Not used by <see cref="Disable"/>:
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
