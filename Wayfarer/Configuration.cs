using Dalamud.Configuration;
using Wayfarer.Core.Input;

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

    public InputModeConfig InputMode { get; set; } = new();
}

/// <summary>Settings for <see cref="InputModeService"/>, shared by every window that adapts to
/// the player's input device.</summary>
public sealed class InputModeConfig
{
    public InputModeOverride Override { get; set; } = InputModeOverride.Auto;

    /// <summary>Set once the player dismisses the one-time hint explaining L1+L3 (LB + left-stick
    /// click on Xbox pads) — Dalamud's global gamepad-nav toggle. Shown in both windows' first
    /// draw until then.</summary>
    public bool ControllerHintDismissed { get; set; }
}

/// <summary>Settings for <see cref="Modules.QuestHelperModule"/>. There is no "show widget"
/// flag here — the widget's visibility while the module is enabled is the module-level
/// <see cref="WidgetHidden"/> toggle (bound to <c>/way</c>); the module's own enabled state
/// (see <see cref="Modules.IModule.Enabled"/>) governs whether it runs at all.</summary>
public sealed class QuestHelperConfig
{
    public bool ArrowLocked { get; set; }

    public float ArrowScale { get; set; } = 1.0f;

    /// <summary>Multiplies the widget's font scale via ImGui.SetWindowFontScale — independent of
    /// <see cref="ArrowScale"/>, which only sizes the arrow graphic. 0.8–2.0.</summary>
    public float TextScale { get; set; } = 1.0f;

    public bool ArrowHideInCombat { get; set; } = true;

    public bool ArrowHideInDuty { get; set; } = true;

    public bool ClickTeleportEnabled { get; set; } = true;

    /// <summary>Toggled by <c>/way</c>; checked by <c>ArrowWindow.DrawConditions</c>.</summary>
    public bool WidgetHidden { get; set; }

    /// <summary>Controls <see cref="ContextMenuActions"/>'s gating: true (the default) shows the
    /// "Wayfarer" submenu on ANY Default-type context menu (any NPC/nameplate right-click, or a
    /// controller subcommand menu) — self-target-only gating turned out unusable on a real HUD (no
    /// solo party frame, finicky self-model right-click, F1-self-targeting rejected as a
    /// workaround). False restores the original self-target-only behavior (own nameplate/
    /// portrait/party-list row) for players who'd rather not see the submenu everywhere.</summary>
    public bool MenuEverywhere { get; set; } = true;
}

/// <summary>Settings for <see cref="Modules.UnlockChecklistModule"/>. Reserved for future use —
/// the module currently has no configurable options beyond enable/disable.</summary>
public sealed class UnlockChecklistConfig
{
}
