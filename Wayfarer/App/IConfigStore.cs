namespace Wayfarer.App;

/// <summary>One JSON file per named config in the plugin's config folder. The app keeps its own in
/// <c>app.json</c>; a module that has settings keeps its own under its own name, so a module's
/// settings live and die with the module and the app never has a field for them.</summary>
internal interface IConfigStore
{
    /// <summary>The config saved under <paramref name="name"/>, or a fresh one when there is no
    /// file yet or the file cannot be read. A file that cannot be read is logged and left alone
    /// until the next save overwrites it.</summary>
    /// <typeparam name="T">The config's shape.</typeparam>
    T Load<T>(string name)
        where T : class, new();

    /// <summary>Writes <paramref name="value"/> as <c>&lt;name&gt;.json</c>, replacing what was there.</summary>
    /// <typeparam name="T">The config's shape.</typeparam>
    void Save<T>(string name, T value)
        where T : class;
}
