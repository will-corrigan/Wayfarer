using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Utility;
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
    // Order matches ContextMenuMode's declaration order (Never = 0, ControllerOnly = 1,
    // Always = 2) — ImGui.Combo indexes by position, not enum value name.
    private static readonly string[] ContextMenuModeLabels = ["Never (default)", "Controller mode only", "Always"];

    public string Name => "Quest Helper";

    public string Description => "An on-screen arrow that guides you to your quest objective, with teleport and aethernet routing.";

    public bool Enabled { get; private set; }

    internal QuestNavigator Navigator { get; } = navigator;

    public void Enable()
    {
        Enabled = true;
        framework.Update += Navigator.OnUpdate;
        windows.AddWindow(arrowWindow);
        commands.AddHandler("/way", new((_, _) =>
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
        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("Arrow size", ref scale, 0.5f, 2.0f, "%.1fx"))
        {
            cfg.ArrowScale = scale;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            saveConfig();
        }

        var textScale = cfg.TextScale;
        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("Text size", ref textScale, 0.8f, 2.0f, "%.1fx"))
        {
            cfg.TextScale = textScale;
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
        if (ImGui.Checkbox("Click-to-teleport (the plugin's only server-affecting action)", ref clickTp))
        {
            cfg.ClickTeleportEnabled = clickTp;
            saveConfig();
        }

        DrawContextMenuModeCombo();
    }

    public void Dispose()
    {
        if (Enabled)
        {
            Disable();
        }
    }

    /// <summary>Parked feature (see <see cref="ContextMenuMode"/>'s doc comment) — kept
    /// configurable rather than removed outright since "Controller mode only" still has
    /// real value (a native, d-pad-navigable action surface the widget can't offer).</summary>
    private void DrawContextMenuModeCombo()
    {
        ImGui.TextUnformatted("Show Wayfarer in right-click menus");
        var current = (int)cfg.MenuMode;
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##contextMenuMode", ref current, ContextMenuModeLabels, ContextMenuModeLabels.Length))
        {
            cfg.MenuMode = (ContextMenuMode)current;
            saveConfig();
        }
    }
}
