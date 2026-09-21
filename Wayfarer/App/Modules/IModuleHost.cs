namespace Wayfarer.App.Modules;

/// <summary>Every module the container holds, and whether each is on. The settings window reads
/// this and tells it when a switch has moved; the modules themselves never ask.</summary>
internal interface IModuleHost
{
    /// <summary>Every registered module, in registration order.</summary>
    IReadOnlyList<IModule> Modules { get; }

    /// <summary>Whether any of what the module offers is switched on, which is what being on
    /// means. There is nothing else to be: a module has no switch of its own.</summary>
    bool IsOn(IModule module);

    /// <summary>Brings the module into line with its settings, after one of them has moved.</summary>
    Task RefreshAsync(IModule module);
}
