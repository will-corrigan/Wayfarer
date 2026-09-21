namespace Wayfarer.App.Modules;

/// <summary>A part of what Wayfarer does: quests, hunting, unlocks. The app knows a module only by
/// this — its name, its picture, and how to bring itself into line with its own settings — never
/// by type.
///
/// <para>A module is not something the player switches. It is on while any of what it offers is
/// switched on, and off when none of it is, so nobody has to know what a module is to use one:
/// they switch the thing they want and the module follows.</para></summary>
internal interface IModule
{
    /// <summary>The name the settings window shows. Stable: it is what the player learns the part
    /// of the plugin by.</summary>
    string Name { get; }

    /// <summary>The game's own picture for what this module is about, shown beside its name. The
    /// module chooses it, because the module is the only thing that knows what it is about; a list
    /// of pictures kept by the settings window would be a second place to remember a new module
    /// in.</summary>
    uint Icon { get; }

    /// <summary>One line under the name in the settings window saying what the module does.</summary>
    string Description { get; }

    /// <summary>What the player can switch. Everything a module offers is one of these, including
    /// the thing the module is chiefly for: a module is on while any of them is, so a module whose
    /// main work had no switch could never be turned off, and one with no switches at all would
    /// never come on.</summary>
    IReadOnlyList<ModuleSetting> Settings => [];

    /// <summary>Brings every part of the module into line with its own settings: what is switched
    /// on runs, what is switched off does not. Called once on load and again after each switch, so
    /// it has to be safe to call on a module that is already exactly as it should be.</summary>
    Task ApplyAsync();

    /// <summary>Takes the whole module down whatever its settings say, releasing focus,
    /// subscriptions and anything it put on screen. Called on unload.</summary>
    Task StopAsync();
}
