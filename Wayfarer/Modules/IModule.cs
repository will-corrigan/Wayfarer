namespace Wayfarer.Modules;

/// <summary>A self-contained feature (quest arrow, unlock tracker, …) that the plugin core hosts
/// without depending on its internals. <see cref="ModuleRegistry"/> owns the enable/disable
/// lifecycle and config-driven wiring; the module itself owns its state and draws its own config
/// section.</summary>
public interface IModule : IDisposable
{
    /// <summary>Short display name shown as the config checkbox label.</summary>
    string Name { get; }

    /// <summary>One-line description shown under the checkbox.</summary>
    string Description { get; }

    /// <summary>Whether the module is currently active. Modules are constructed inactive
    /// (<see langword="false"/>); <see cref="ModuleRegistry"/> is solely responsible for
    /// transitioning a module to active via <see cref="Enable"/> once it has resolved the
    /// desired state from saved config.</summary>
    bool Enabled { get; }

    /// <summary>Activates the module (subscribes to events, starts timers, etc.). Never called by
    /// the module's own constructor — modules are constructed inactive, and only
    /// <see cref="ModuleRegistry"/> calls this, after registration.</summary>
    void Enable();

    /// <summary>Deactivates the module, undoing everything <see cref="Enable"/> did.</summary>
    void Disable();

    // Modules deliberately do NOT draw their own settings. Every setting Wayfarer has is declared
    // once in Settings/SettingsCatalog and rendered by whichever presentation is on screen — the
    // old per-module DrawConfig was reachable only from Dalamud's mouse-driven config window,
    // which is how a controller player ended up with settings they could see the effects of but
    // never change.
}
