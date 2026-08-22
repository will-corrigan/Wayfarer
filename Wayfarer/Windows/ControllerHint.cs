using Dalamud.Bindings.ImGui;

namespace Wayfarer.Windows;

/// <summary>The one-time "LB + Left Stick click enables gamepad navigation" hint drawn at the
/// top of both <see cref="ArrowWindow"/> and <see cref="UnlockWindow"/>'s first draw, until
/// dismissed. Backed by a single shared <see cref="InputModeConfig.ControllerHintDismissed"/>
/// flag — dismissing it in either window dismisses it in both, permanently (persisted config).</summary>
internal static class ControllerHint
{
    public static void Draw(InputModeConfig cfg, Action saveConfig)
    {
        if (cfg.ControllerHintDismissed)
        {
            return;
        }

        ImGui.TextWrapped("Playing with a controller? Hold LB and click the Left Stick to turn on gamepad navigation for plugin windows.");
        if (ImGui.SmallButton("Got it##dismissControllerHint"))
        {
            cfg.ControllerHintDismissed = true;
            saveConfig();
        }

        ImGui.Separator();
        ImGui.Spacing();
    }
}
