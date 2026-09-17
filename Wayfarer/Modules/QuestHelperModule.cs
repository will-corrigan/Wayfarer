using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Guidance.Sources;

namespace Wayfarer.Modules;

/// <summary>Guides the player to their followed quest's objective, with teleport and city-aethernet
/// routing. Exposes <see cref="Navigator"/> so the surfaces that render guidance can read it.</summary>
internal sealed class QuestHelperModule(
    IFramework framework,
    ICommandManager commands,
    QuestHelperConfig cfg,
    Action saveConfig,
    QuestNavigator navigator,
    IGuidanceArbiter arbiter,
    QuestObjectiveSource questSource) : IModule
{
    public string Name => "Quest Helper";

    public string Description => "An arrow to your quest objective, with teleport and aethernet routing.";

    public bool Enabled { get; private set; }

    internal QuestNavigator Navigator { get; } = navigator;

    public void Enable()
    {
        Enabled = true;

        // Registered last-in-wins order does not matter for an ambient source, but registration
        // itself does: while this module is disabled there is no followed quest to fall back to,
        // and the arrow correctly shows nothing rather than state nobody is maintaining.
        arbiter.Register(questSource);
        framework.Update += Navigator.OnUpdate;
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
        framework.Update -= Navigator.OnUpdate;
        arbiter.Unregister(questSource);
    }

    public void Dispose()
    {
        if (Enabled)
        {
            Disable();
        }
    }
}
