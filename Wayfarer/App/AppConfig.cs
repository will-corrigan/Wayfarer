namespace Wayfarer.App;

/// <summary>The app's own settings: which modules are on, by name. Saved as <c>app.json</c>.
/// Nothing about any module's own settings lives here.</summary>
internal sealed class AppConfig
{
    /// <summary>Bumped when the shape changes, so an old file can be recognised and migrated.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Whether the enabled set has ever been written. On a first run every module is
    /// switched on; after that the set is exactly what the player left it as, including empty.</summary>
    public bool Initialised { get; set; }

    /// <summary>The names of the modules that are on.</summary>
    public HashSet<string> EnabledModules { get; set; } = new(StringComparer.Ordinal);
}
