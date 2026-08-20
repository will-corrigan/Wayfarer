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

    /// <summary>Draws the module's own config section. Only called while the module is enabled.</summary>
    void DrawConfig();
}
