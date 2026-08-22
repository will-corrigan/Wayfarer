using Dalamud.Bindings.ImGui;

namespace Wayfarer.Windows;

/// <summary>The one-time "enable gamepad navigation, then open the hub" hint drawn at the top of
/// both <see cref="ArrowWindow"/> and <see cref="UnlockWindow"/>'s first draw, until dismissed.
/// Backed by a single shared <see cref="InputModeConfig.ControllerHintDismissed"/> flag —
/// dismissing it in either window dismisses it in both, permanently (persisted config). Describes
/// the full controller entry-point flow (spec: controller wave task 5): enable Dalamud's gamepad
/// nav, then confirm the widget's "Open Wayfarer ▸" row to reach the native hub — using whichever
/// pad's button labels <see cref="InputModeService"/> currently detects.</summary>
internal static class ControllerHint
{
    public static void Draw(InputModeConfig cfg, InputModeService inputMode, Action saveConfig)
    {
        if (cfg.ControllerHintDismissed)
        {
            return;
        }

        var enableCombo = inputMode.IsPlayStationPad ? "L1 + L3" : "LB + Left Stick click";
        ImGui.TextWrapped(
            $"Playing with a controller? Press {enableCombo} to turn on gamepad navigation, then " +
            $"highlight \"Open Wayfarer ▸\" below and press {inputMode.Glyphs.Confirm} to open the full menu.");
        if (ImGui.SmallButton("Got it##dismissControllerHint"))
        {
            cfg.ControllerHintDismissed = true;
            saveConfig();
        }

        ImGui.Separator();
        ImGui.Spacing();
    }
}
