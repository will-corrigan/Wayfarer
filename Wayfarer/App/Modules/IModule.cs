namespace Wayfarer.App.Modules;

/// <summary>A feature the player can switch on and off: quests, hunting, unlocks. The app knows a
/// module only by this — its name, and how to start and stop it — never by type. Which modules
/// are on is the app's setting, kept by name; what a module does when on is its own business.
///
/// <para>Starting and stopping are live: the settings window flips a module without a reload, so
/// <see cref="EnableAsync"/> has to bring the module fully up from nothing and
/// <see cref="DisableAsync"/> has to take it fully down, releasing focus, subscriptions and
/// anything on screen.</para></summary>
internal interface IModule
{
    /// <summary>The name the settings window shows and the enabled set is keyed by. Stable: renaming
    /// it silently switches the module off for everyone who had it on.</summary>
    string Name { get; }

    /// <summary>One line under the name in the settings window saying what the module does.</summary>
    string Description { get; }

    /// <summary>What the player can switch about the module itself, shown under its own switch.
    /// A module with nothing to configure says nothing.</summary>
    IReadOnlyList<ModuleSetting> Settings => [];

    /// <summary>Brings the module up. Called once when the plugin loads if the module is on, and
    /// again each time the player switches it on.</summary>
    Task EnableAsync();

    /// <summary>Takes the module down. Called each time the player switches it off, and on unload
    /// for every module that is up.</summary>
    Task DisableAsync();
}
