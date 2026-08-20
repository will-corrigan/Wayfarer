using Dalamud.Bindings.ImGui;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Tracks every quest-unlockable feature, mount and dungeon the player can pick up
/// right now, and lets the checklist route <see cref="QuestHelperModule"/>'s arrow to the
/// quest givers (task-5-brief.md delta 3).</summary>
public sealed class UnlockChecklistModule : IModule
{
    private readonly Plugin plugin;
    private readonly UnlockWindow unlockWindow;

    public UnlockChecklistModule(Plugin plugin)
    {
        this.plugin = plugin;
        Unlocks = new UnlockService(plugin);
        unlockWindow = new UnlockWindow(plugin, Unlocks);
    }

    public string Name => "Unlock Checklist";

    public string Description => "Tracks every quest-unlockable feature, mount and dungeon you can pick up right now, and routes you to the quest givers.";

    public bool Enabled { get; private set; }

    internal UnlockService Unlocks { get; }

    internal UnlockWindow Window => unlockWindow;

    public void Enable()
    {
        Enabled = true;
        plugin.Framework.Update += Unlocks.OnFrameworkUpdate;
        if (plugin.Modules.Get<QuestHelperModule>() is { } questHelper)
        {
            questHelper.Navigator.OnPickupAdvanced += Unlocks.OnPickupAdvanced;
        }

        plugin.Windows.AddWindow(unlockWindow);
    }

    public void Disable()
    {
        Enabled = false;
        plugin.Windows.RemoveWindow(unlockWindow);
        if (plugin.Modules.Get<QuestHelperModule>() is { } questHelper)
        {
            questHelper.Navigator.OnPickupAdvanced -= Unlocks.OnPickupAdvanced;
        }

        plugin.Framework.Update -= Unlocks.OnFrameworkUpdate;
    }

    public void DrawConfig()
    {
        if (ImGui.Button("Open checklist"))
        {
            unlockWindow.IsOpen = true;
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
