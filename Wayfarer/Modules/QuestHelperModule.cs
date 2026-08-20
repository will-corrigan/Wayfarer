using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Draws an on-screen arrow guiding the player to their followed quest's objective,
/// with teleport and city-aethernet routing. Exposes <see cref="Navigator"/> so
/// <see cref="UnlockChecklistModule"/> can route the arrow to unlock-quest pickups
/// (task-5-brief.md delta 3).</summary>
internal sealed class QuestHelperModule(
    IFramework framework,
    WindowSystem windows,
    ICommandManager commands,
    QuestHelperConfig cfg,
    Action saveConfig,
    QuestNavigator navigator,
    ArrowWindow arrowWindow) : IModule
{
    public string Name => "Quest Helper";

    public string Description => "An on-screen arrow that guides you to your quest objective, with teleport and aethernet routing.";

    public bool Enabled { get; private set; }

    internal QuestNavigator Navigator { get; } = navigator;

    public void Enable()
    {
        Enabled = true;
        framework.Update += Navigator.OnUpdate;
        windows.AddWindow(arrowWindow);
        commands.AddHandler("/way", new CommandInfo((_, _) =>
        {
            cfg.WidgetHidden = !cfg.WidgetHidden;
            saveConfig();
        })
        { HelpMessage = "Toggle the quest arrow widget" });
    }

    public void Disable()
    {
        Enabled = false;
        commands.RemoveHandler("/way");
        windows.RemoveWindow(arrowWindow);
        framework.Update -= Navigator.OnUpdate;
    }

    public void DrawConfig()
    {
        var arrowLocked = cfg.ArrowLocked;
        if (ImGui.Checkbox("Lock widget position", ref arrowLocked))
        {
            cfg.ArrowLocked = arrowLocked;
            saveConfig();
        }

        var scale = cfg.ArrowScale;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderFloat("Arrow size", ref scale, 0.5f, 2.0f, "%.1fx"))
        {
            cfg.ArrowScale = scale;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            saveConfig();
        }

        var hideCombat = cfg.ArrowHideInCombat;
        if (ImGui.Checkbox("Hide in combat", ref hideCombat))
        {
            cfg.ArrowHideInCombat = hideCombat;
            saveConfig();
        }

        var hideDuty = cfg.ArrowHideInDuty;
        if (ImGui.Checkbox("Hide in duties", ref hideDuty))
        {
            cfg.ArrowHideInDuty = hideDuty;
            saveConfig();
        }

        var clickTp = cfg.ClickTeleportEnabled;
        if (ImGui.Checkbox("Click-to-teleport (the plugin's only game action)", ref clickTp))
        {
            cfg.ClickTeleportEnabled = clickTp;
            saveConfig();
        }
    }

    public void Dispose()
    {
        if (Enabled)
        {
            Disable();
        }
    }
}
