using Dalamud.Configuration;

namespace Wayfarer;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Per-module enabled flag, keyed by <see cref="Modules.IModule.Name"/>. A missing key
    /// means "use the module's own default" — see <see cref="Modules.ModuleRegistry.Register"/>.
    /// Nested per-module config classes are added alongside the modules that need them.</summary>
    public Dictionary<string, bool> ModuleEnabled { get; set; } = [];

    public QuestHelperConfig QuestHelper { get; set; } = new();

    public UnlockChecklistConfig UnlockChecklist { get; set; } = new();
}

/// <summary>Settings for <see cref="Modules.QuestHelperModule"/>. There is no "show widget"
/// flag here — the widget's visibility while the module is enabled is the module-level
/// <see cref="WidgetHidden"/> toggle (bound to <c>/way</c>); the module's own enabled state
/// (see <see cref="Modules.IModule.Enabled"/>) governs whether it runs at all.</summary>
public sealed class QuestHelperConfig
{
    public bool ArrowLocked { get; set; }

    public float ArrowScale { get; set; } = 1.0f;

    public bool ArrowHideInCombat { get; set; } = true;

    public bool ArrowHideInDuty { get; set; } = true;

    public bool ClickTeleportEnabled { get; set; } = true;

    /// <summary>Toggled by <c>/way</c>; checked by <c>ArrowWindow.DrawConditions</c>.</summary>
    public bool WidgetHidden { get; set; }
}

/// <summary>Settings for <see cref="Modules.UnlockChecklistModule"/>. Reserved for future use —
/// the module currently has no configurable options beyond enable/disable.</summary>
public sealed class UnlockChecklistConfig
{
}
