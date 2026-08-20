using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Windows;

namespace Wayfarer.Modules;

/// <summary>Tracks every quest-unlockable feature, mount and dungeon the player can pick up
/// right now, and lets the checklist route <see cref="QuestHelperModule"/>'s arrow to the
/// quest givers (task-5-brief.md delta 3).</summary>
public sealed class UnlockChecklistModule : IModule
{
    private readonly IFramework framework;
    private readonly WindowSystem windows;
    private readonly ModuleRegistry modules;

    internal UnlockChecklistModule(
        IFramework framework,
        WindowSystem windows,
        ModuleRegistry modules,
        UnlockService unlocks,
        UnlockWindow unlockWindow)
    {
        this.framework = framework;
        this.windows = windows;
        this.modules = modules;
        Unlocks = unlocks;
        Window = unlockWindow;
    }

    public string Name => "Unlock Checklist";

    public string Description => "Tracks every quest-unlockable feature, mount and dungeon you can pick up right now, and routes you to the quest givers.";

    public bool Enabled { get; private set; }

    internal UnlockService Unlocks { get; }

    internal UnlockWindow Window { get; }

    public void Enable()
    {
        Enabled = true;
        framework.Update += Unlocks.OnFrameworkUpdate;
        if (modules.Get<QuestHelperModule>() is { } questHelper)
        {
            questHelper.Navigator.OnPickupAdvanced += Unlocks.OnPickupAdvanced;
        }

        windows.AddWindow(Window);
    }

    public void Disable()
    {
        Enabled = false;
        windows.RemoveWindow(Window);
        if (modules.Get<QuestHelperModule>() is { } questHelper)
        {
            questHelper.Navigator.OnPickupAdvanced -= Unlocks.OnPickupAdvanced;
        }

        framework.Update -= Unlocks.OnFrameworkUpdate;
    }

    public void DrawConfig()
    {
        if (ImGui.Button("Open checklist"))
        {
            Window.IsOpen = true;
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
