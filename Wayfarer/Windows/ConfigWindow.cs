using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

public sealed class ConfigWindow(ModuleRegistry modules, Configuration config, Action saveConfig) : Window("Wayfarer")
{
    public override void Draw()
    {
        foreach (var module in modules.Modules)
        {
            var enabled = module.Enabled;
            if (ImGui.Checkbox(module.Name, ref enabled))
            {
                modules.SetEnabled(module, enabled);
                config.ModuleEnabled[module.Name] = enabled;
                saveConfig();
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
