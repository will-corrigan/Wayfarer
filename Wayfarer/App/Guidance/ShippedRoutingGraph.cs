using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Routing;

namespace Wayfarer.App.Guidance;

/// <summary>The routing graph from the data file shipped beside the plugin's DLL. If the file is
/// missing or unreadable the graph is empty, which is logged once: every route is then a walk on
/// the current map, and nothing else is affected.</summary>
internal static class ShippedRoutingGraph
{
    /// <summary>Reads the routing data shipped beside the plugin's own file. An empty graph is the
    /// answer if the file is missing or will not parse, which guides without routing rather than
    /// failing to load at all.</summary>
    public static RouteGraph Load(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        var path = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, RoutingGraphFile.FileName);
        try
        {
            return RoutingGraphFile.Parse(File.ReadAllText(path)).ToGraph();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            log.Error(ex, $"{RoutingGraphFile.FileName} could not be read, so routes are walks on the current map only.");
            return new RouteGraph([], []);
        }
    }
}
