namespace Wayfarer.App.Modules;

/// <summary>Every module the container holds, and which of them are on. The settings window reads
/// and writes this; the modules themselves never ask.</summary>
internal interface IModuleHost
{
    /// <summary>Every registered module, in registration order.</summary>
    IReadOnlyList<IModule> Modules { get; }

    /// <summary>Whether the module is currently up.</summary>
    bool IsEnabled(IModule module);

    /// <summary>Brings the module up or takes it down, and remembers the choice.</summary>
    Task SetEnabledAsync(IModule module, bool enabled);
}
