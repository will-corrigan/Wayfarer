using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Wayfarer.Core.Ui;
using Wayfarer.Settings;

namespace Wayfarer.Windows;

/// <summary>The ImGui rendering of <see cref="SettingsCatalog"/> — the same settings, in the same
/// order, as the native window's Settings tab, because both read one declaration.
///
/// This is a <b>fallback surface</b>, not a destination: everything Wayfarer does now lives in the
/// one native window, and both Dalamud's cog and its main button open that. This exists for the
/// case where the native window cannot be created at all, so a player is never left with no way to
/// turn a setting off.</summary>
public sealed class ConfigWindow : Window
{
    private readonly SettingsCatalog settings;

    internal ConfigWindow(SettingsCatalog settings)
        : base("Wayfarer")
    {
        this.settings = settings;
    }

    public override void Draw()
    {
        foreach (var section in settings.Build())
        {
            ImGui.TextUnformatted(section.Title);
            ImGui.Separator();
            foreach (var setting in section.Settings)
            {
                DrawSetting(setting);
            }

            ImGui.Spacing();
        }
    }

    private static void DrawSetting(SettingDefinition setting)
    {
        switch (setting.Kind)
        {
            case SettingKind.Toggle:
                DrawToggle(setting);
                break;
            case SettingKind.Scale:
                DrawScale(setting);
                break;
            default:
                DrawChoice(setting);
                break;
        }

        if (setting.Description is { Length: > 0 } description)
        {
            ImGui.Indent();
            ImGui.TextDisabled(description);
            ImGui.Unindent();
        }
    }

    private static void DrawToggle(SettingDefinition setting)
    {
        var value = setting.ReadFlag?.Invoke() ?? false;
        if (ImGui.Checkbox($"{setting.Label}##{setting.Id}", ref value))
        {
            setting.WriteFlag?.Invoke(value);
        }
    }

    private static void DrawScale(SettingDefinition setting)
    {
        var value = setting.ReadValue?.Invoke() ?? setting.Minimum;
        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat($"{setting.Label}##{setting.Id}", ref value, setting.Minimum, setting.Maximum, "%.1fx"))
        {
            setting.WriteValue?.Invoke(value);
        }
    }

    private static void DrawChoice(SettingDefinition setting)
    {
        var current = setting.ReadOption?.Invoke() ?? 0;
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo($"{setting.Label}##{setting.Id}", ref current, [.. setting.Options], setting.Options.Count))
        {
            setting.WriteOption?.Invoke(current);
        }
    }
}
