using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Draws an on-screen arrow guiding the player to their followed quest's objective,
/// with teleport and city-aethernet routing. Exposes <see cref="Navigator"/> so
/// <see cref="UnlockChecklistModule"/> can route the arrow to unlock-quest pickups
/// (task-5-brief.md delta 3).</summary>
public sealed class QuestHelperModule : IModule
{
    private readonly Plugin plugin;
    private readonly ArrowWindow arrowWindow;

    public QuestHelperModule(Plugin plugin)
    {
        this.plugin = plugin;
        Navigator = new QuestNavigator(plugin);
        arrowWindow = new ArrowWindow(plugin, Navigator);
    }

    public string Name => "Quest Helper";

    public string Description => "An on-screen arrow that guides you to your quest objective, with teleport and aethernet routing.";

    public bool Enabled { get; private set; }

    internal QuestNavigator Navigator { get; }

    public void Enable()
    {
        Enabled = true;
        plugin.Framework.Update += Navigator.OnUpdate;
        plugin.Windows.AddWindow(arrowWindow);
        plugin.Commands.AddHandler("/way", new CommandInfo((_, _) =>
        {
            plugin.Config.QuestHelper.WidgetHidden = !plugin.Config.QuestHelper.WidgetHidden;
            plugin.SaveConfig();
        })
        { HelpMessage = "Toggle the quest arrow widget" });
    }

    public void Disable()
    {
        Enabled = false;
        plugin.Commands.RemoveHandler("/way");
        plugin.Windows.RemoveWindow(arrowWindow);
        plugin.Framework.Update -= Navigator.OnUpdate;
    }

    public void DrawConfig()
    {
        var cfg = plugin.Config.QuestHelper;

        var arrowLocked = cfg.ArrowLocked;
        if (ImGui.Checkbox("Lock widget position", ref arrowLocked))
        {
            cfg.ArrowLocked = arrowLocked;
            plugin.SaveConfig();
        }

        var scale = cfg.ArrowScale;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderFloat("Arrow size", ref scale, 0.5f, 2.0f, "%.1fx"))
        {
            cfg.ArrowScale = scale;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            plugin.SaveConfig();
        }

        var hideCombat = cfg.ArrowHideInCombat;
        if (ImGui.Checkbox("Hide in combat", ref hideCombat))
        {
            cfg.ArrowHideInCombat = hideCombat;
            plugin.SaveConfig();
        }

        var hideDuty = cfg.ArrowHideInDuty;
        if (ImGui.Checkbox("Hide in duties", ref hideDuty))
        {
            cfg.ArrowHideInDuty = hideDuty;
            plugin.SaveConfig();
        }

        var clickTp = cfg.ClickTeleportEnabled;
        if (ImGui.Checkbox("Click-to-teleport (the plugin's only game action)", ref clickTp))
        {
            cfg.ClickTeleportEnabled = clickTp;
            plugin.SaveConfig();
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
