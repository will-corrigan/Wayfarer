using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Wayfarer.Windows;

public sealed class ConfigWindow(Plugin plugin) : Window("Wayfarer")
{
    public override void Draw()
    {
        foreach (var module in plugin.Modules.Modules)
        {
            var enabled = module.Enabled;
            if (ImGui.Checkbox(module.Name, ref enabled))
            {
                plugin.Modules.SetEnabled(module, enabled);
                plugin.Config.ModuleEnabled[module.Name] = enabled;
                plugin.SaveConfig();
            }

            ImGui.TextDisabled(module.Description);

            if (module.Enabled)
            {
                ImGui.Indent();
                module.DrawConfig();
                ImGui.Unindent();
            }
        }
    }
}
