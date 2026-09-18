using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Wayfarer.App;

/// <summary>The config folder Dalamud gives the plugin, one indented JSON file per name.</summary>
internal sealed class ConfigStore(IDalamudPluginInterface pluginInterface, IPluginLog log) : IConfigStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <inheritdoc/>
    public T Load<T>(string name)
        where T : class, new()
    {
        var path = PathFor(name);
        if (!File.Exists(path))
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            log.Warning(ex, $"Wayfarer: {name}.json could not be read, so its defaults are in use until it is next saved.");
            return new T();
        }
    }

    /// <inheritdoc/>
    public void Save<T>(string name, T value)
        where T : class
    {
        var path = PathFor(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }

    private string PathFor(string name) => Path.Combine(pluginInterface.ConfigDirectory.FullName, $"{name}.json");
}
