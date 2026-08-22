using Dalamud.Bindings.ImGui;

namespace Wayfarer.Windows;

/// <summary>The one-time "L1 + L3 enables gamepad navigation" hint drawn at the top of both
/// <see cref="ArrowWindow"/> and <see cref="UnlockWindow"/>'s first draw, until dismissed.
/// Backed by a single shared <see cref="InputModeConfig.ControllerHintDismissed"/> flag —
/// dismissing it in either window dismisses it in both, permanently (persisted config). Named
/// by Dalamud's own brand-neutral button enum (<c>GamepadButtons.L1</c>/<c>L3</c> — see
/// <see cref="InputModeService"/>'s reflection findings on why button-brand detection isn't
/// possible), with the Xbox-labeled shorthand alongside for players who only know it that way.</summary>
internal static class ControllerHint
{
    public static void Draw(InputModeConfig cfg, Action saveConfig)
    {
        if (cfg.ControllerHintDismissed)
        {
            return;
        }

        ImGui.TextWrapped("Playing with a controller? Press L1 + L3 (LB + left-stick click on Xbox pads) to turn on gamepad navigation for plugin windows.");
        if (ImGui.SmallButton("Got it##dismissControllerHint"))
        {
            cfg.ControllerHintDismissed = true;
            saveConfig();
        }

        ImGui.Separator();
        ImGui.Spacing();
    }
}
