using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Wayfarer.Core.Input;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

public sealed class ConfigWindow(ModuleRegistry modules, Configuration config, Action saveConfig) : Window("Wayfarer")
{
    private static readonly InputModeOverride[] InputModeOverrides =
        [InputModeOverride.Auto, InputModeOverride.Mouse, InputModeOverride.Controller];

    public override void Draw()
    {
        DrawInputModeSection();
        ImGui.Separator();
        ImGui.Spacing();

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

    /// <summary>Auto/Mouse/Controller override — see <see cref="InputModeArbitrator"/>. Auto
    /// (the default) follows whichever device was used most recently; the other two options pin
    /// the presentation regardless of what the player's hands are doing.</summary>
    private void DrawInputModeSection()
    {
        ImGui.TextUnformatted("Input mode");
        var current = Array.IndexOf(InputModeOverrides, config.InputMode.Override);
        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo(
            "##inputModeOverride",
            ref current,
            ["Auto (follow last input)", "Mouse & keyboard", "Controller"],
            InputModeOverrides.Length))
        {
            config.InputMode.Override = InputModeOverrides[current];
            saveConfig();
        }
    }
}
