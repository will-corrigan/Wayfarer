using Dalamud.Configuration;
using Wayfarer.Core.Input;

namespace Wayfarer;

/// <summary>Gates when <see cref="ContextMenuActions"/> registers its "Wayfarer" submenu on the
/// game's Default context menu. Parked feature (see <see cref="QuestHelperConfig.MenuMode"/>) —
/// an "any right-click menu" design was tried and rejected: it's redundant for mouse players, who
/// already have the clickable widget, so <see cref="ControllerOnly"/> is the only case with
/// real value (a native, d-pad-navigable action surface where the widget's click affordances
/// don't reach), and <see cref="Never"/> is the default until a better entry point is designed.</summary>
public enum ContextMenuMode
{
    Never,
    ControllerOnly,
    Always,
}

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Per-module enabled flag, keyed by <see cref="Modules.IModule.Name"/>. A missing key
    /// means "use the module's own default" — see <see cref="Modules.ModuleRegistry.Register"/>.
    /// Nested per-module config classes are added alongside the modules that need them.</summary>
    public Dictionary<string, bool> ModuleEnabled { get; set; } = [];

    public QuestHelperConfig QuestHelper { get; set; } = new();

    public UnlockChecklistConfig UnlockChecklist { get; set; } = new();

    public HuntingLogConfig HuntingLog { get; set; } = new();

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

    /// <summary>Controls <see cref="ContextMenuActions"/>'s gating. Defaults to <see
    /// cref="ContextMenuMode.Never"/> — the feature is parked pending a different entry-point
    /// design (an "any right-click menu" submenu was tried and rejected as noisy for mouse
    /// players, who already have the clickable widget). See <see cref="ContextMenuMode"/>.</summary>
    public ContextMenuMode MenuMode { get; set; } = ContextMenuMode.Never;
}

/// <summary>Settings for <see cref="Modules.UnlockChecklistModule"/>.</summary>
public sealed class UnlockChecklistConfig
{
    /// <summary>Shows the top 2-3 Available unlocks in the current zone as small lines on
    /// <see cref="Windows.ArrowWindow"/> (spec §4, task A3) — a quick glance that makes opening
    /// the checklist window optional. On by default; absent regardless when the module itself is
    /// disabled.</summary>
    public bool ShowOnWidget { get; set; } = true;
}

/// <summary>Settings for <see cref="Modules.HuntingLogModule"/>.</summary>
public sealed class HuntingLogConfig
{
    /// <summary>Shows the current hunting-log target and its kill count as a small line on
    /// <see cref="Windows.ArrowWindow"/> (spec §4/§5) — a quick glance that makes opening the
    /// hunting log window optional. On by default; absent regardless when the module itself is
    /// disabled.</summary>
    public bool ShowOnWidget { get; set; } = true;
}
