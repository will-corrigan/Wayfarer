using Wayfarer.Core.Input;
using Wayfarer.Core.Ui;
using Wayfarer.Modules;

namespace Wayfarer.Settings;

/// <summary>Every Wayfarer setting, declared exactly once.
///
/// This is the implementation half of the settings bridge (<see cref="SettingDefinition"/> is the
/// abstraction): it knows what the settings mean and how to read and write them, and knows nothing
/// about how any of them are drawn. The native window renders this list; the ImGui fallback renders
/// the same list. Neither may declare a setting of its own, and adding one here makes it appear in
/// both with no further work — which is the fix for the class of defect where a per-module
/// <c>DrawConfig</c> checkbox existed only inside Dalamud's mouse-driven config window and was
/// therefore unreachable for a controller player.</summary>
internal sealed class SettingsCatalog(Configuration config, ModuleRegistry modules, Action saveConfig)
{
    private static readonly string[] InputModeLabels = ["Automatic", "Mouse and keyboard", "Controller"];

    private static readonly InputModeOverride[] InputModeValues =
        [InputModeOverride.Auto, InputModeOverride.Mouse, InputModeOverride.Controller];

    private static readonly string[] ReadoutPositionLabels =
        ["Follow the quest tracker", "Upper left", "Upper right", "Lower left", "Lower right"];

    private static readonly ReadoutPosition[] ReadoutPositionValues =
    [
        ReadoutPosition.FollowQuestTracker,
        ReadoutPosition.TopLeft,
        ReadoutPosition.TopRight,
        ReadoutPosition.BottomLeft,
        ReadoutPosition.BottomRight,
    ];

    private static readonly string[] WindowPositionLabels =
        ["Center", "Upper left", "Upper right", "Lower left", "Lower right"];

    private static readonly HubPositionPreset[] WindowPositionValues =
    [
        HubPositionPreset.Center,
        HubPositionPreset.TopLeft,
        HubPositionPreset.TopRight,
        HubPositionPreset.BottomLeft,
        HubPositionPreset.BottomRight,
    ];

    // Every id here is one the game itself emits above a nameplate, taken from its own
    // EventIconType / EventIconPriority sheets. Offered as a choice rather than fixed because
    // "does this read as a real quest marker over a monster" is the one question in this whole
    // feature that only an eye can answer — see GuidanceConfig.NamePlateMarkerIcon.
    private static readonly string[] MarkerIconLabels =
        ["Quest in progress", "Quest available", "Main scenario", "Look here"];

    private static readonly int[] MarkerIconValues = [71223, 71221, 71203, 60094];

    // Positional, matching ContextMenuMode's declaration order.
    private static readonly string[] ContextMenuLabels = ["Never", "On a controller", "Always"];

    /// <summary>Raised when the window-position setting changes, so the window can move at once
    /// rather than on the next open.</summary>
    public Action<HubPositionPreset>? OnWindowPositionChanged { get; set; }

    /// <summary>Rebuilt on demand rather than cached: the module list is fixed after registration,
    /// but the delegates close over live config objects and every presentation wants the current
    /// values at the moment it draws.</summary>
    public IReadOnlyList<SettingSection> Build() =>
    [
        new SettingSection("Features", BuildModuleSettings()),
        new SettingSection("Guidance", BuildGuidanceSettings()),
        new SettingSection("Readout", BuildReadoutSettings()),
        new SettingSection("Controls", BuildControlSettings()),
    ];

    // A stored value that is no longer offered (a hand-edited config, or an option removed in a
    // later version) reads back as the first entry rather than as an out-of-range index.
    private static int IndexOf(int[] values, int value)
    {
        var index = Array.IndexOf(values, value);
        return index < 0 ? 0 : index;
    }

    private List<SettingDefinition> BuildModuleSettings()
    {
        var settings = new List<SettingDefinition>();
        foreach (var module in modules.Modules)
        {
            var owned = module;
            settings.Add(new SettingDefinition
            {
                Id = $"module.{owned.Name}",
                Label = owned.Name,
                Description = owned.Description,
                Kind = SettingKind.Toggle,
                ReadFlag = () => owned.Enabled,
                WriteFlag = value =>
                {
                    modules.SetEnabled(owned, value);
                    config.ModuleEnabled[owned.Name] = value;
                    saveConfig();
                },
            });
        }

        return settings;
    }

    private IReadOnlyList<SettingDefinition> BuildGuidanceSettings() =>
    [
        new SettingDefinition
        {
            Id = "guidance.mapFlag",
            Label = "Mark the target with the map flag",
            Description = "Restores your own flag when the route ends.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.Guidance.MarkObjectiveWithMapFlag,
            WriteFlag = Write(value => config.Guidance.MarkObjectiveWithMapFlag = value),
        },
        new SettingDefinition
        {
            Id = "guidance.namePlates",
            Label = "Mark targets above their heads",
            Description = "Uses the game's own quest marker. Never replaces a marker the game put there.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.Guidance.MarkTargetsOnNameplates,
            WriteFlag = Write(value => config.Guidance.MarkTargetsOnNameplates = value),
        },
        new SettingDefinition
        {
            Id = "guidance.markerIcon",
            Label = "Marker",
            Kind = SettingKind.Choice,
            Options = MarkerIconLabels,
            ReadOption = () => IndexOf(MarkerIconValues, config.Guidance.NamePlateMarkerIcon),
            WriteOption = Write(index => config.Guidance.NamePlateMarkerIcon = MarkerIconValues[index]),
        },
        new SettingDefinition
        {
            Id = "guidance.clickTeleport",
            Label = "Teleport when the readout's aetheryte is clicked",
            Description = "The only thing Wayfarer ever does that the server sees.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.ClickTeleportEnabled,
            WriteFlag = Write(value => config.QuestHelper.ClickTeleportEnabled = value),
        },
    ];

    private IReadOnlyList<SettingDefinition> BuildReadoutSettings() =>
        [.. BuildReadoutAppearance(), .. BuildReadoutContent()];

    private IReadOnlyList<SettingDefinition> BuildReadoutAppearance() =>
    [
        new SettingDefinition
        {
            Id = "readout.show",
            Label = "Show the readout",
            Kind = SettingKind.Toggle,
            ReadFlag = () => !config.QuestHelper.WidgetHidden,
            WriteFlag = Write(value => config.QuestHelper.WidgetHidden = !value),
        },
        new SettingDefinition
        {
            Id = "readout.position",
            Label = "Position",
            Kind = SettingKind.Choice,
            Options = ReadoutPositionLabels,
            ReadOption = () => Array.IndexOf(ReadoutPositionValues, config.QuestHelper.ReadoutPosition),
            WriteOption = Write(index => config.QuestHelper.ReadoutPosition = ReadoutPositionValues[index]),
        },
        new SettingDefinition
        {
            Id = "readout.textScale",
            Label = "Text size",
            Kind = SettingKind.Scale,
            Minimum = 0.8f,
            Maximum = 2.0f,
            Step = 0.1f,
            ReadValue = () => config.QuestHelper.TextScale,
            WriteValue = WriteNumber(value => config.QuestHelper.TextScale = value),
        },
        new SettingDefinition
        {
            Id = "readout.arrowScale",
            Label = "Arrow size",
            Kind = SettingKind.Scale,
            Minimum = 0.5f,
            Maximum = 2.0f,
            Step = 0.1f,
            ReadValue = () => config.QuestHelper.ArrowScale,
            WriteValue = WriteNumber(value => config.QuestHelper.ArrowScale = value),
        },
    ];

    private IReadOnlyList<SettingDefinition> BuildReadoutContent() =>
    [
        new SettingDefinition
        {
            Id = "readout.unlocks",
            Label = "Show nearby unlocks",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.UnlockChecklist.ShowOnWidget,
            WriteFlag = Write(value => config.UnlockChecklist.ShowOnWidget = value),
        },
        new SettingDefinition
        {
            Id = "readout.hunting",
            Label = "Show hunting progress",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.HuntingLog.ShowOnWidget,
            WriteFlag = Write(value => config.HuntingLog.ShowOnWidget = value),
        },
        new SettingDefinition
        {
            Id = "readout.hideInCombat",
            Label = "Hide in combat",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.ArrowHideInCombat,
            WriteFlag = Write(value => config.QuestHelper.ArrowHideInCombat = value),
        },
        new SettingDefinition
        {
            Id = "readout.hideInDuty",
            Label = "Hide in duties",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.ArrowHideInDuty,
            WriteFlag = Write(value => config.QuestHelper.ArrowHideInDuty = value),
        },
        new SettingDefinition
        {
            Id = "readout.native",
            Label = "Draw the readout with the game's own text",
            Description = "Turn this off to fall back to the old plugin-drawn widget.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.UseNativeReadout,
            WriteFlag = Write(value => config.QuestHelper.UseNativeReadout = value),
        },
    ];

    private IReadOnlyList<SettingDefinition> BuildControlSettings() =>
    [
        new SettingDefinition
        {
            Id = "input.mode",
            Label = "Input",
            Description = "Automatic follows whichever device you used last.",
            Kind = SettingKind.Choice,
            Options = InputModeLabels,
            ReadOption = () => Array.IndexOf(InputModeValues, config.InputMode.Override),
            WriteOption = Write(index => config.InputMode.Override = InputModeValues[index]),
        },
        new SettingDefinition
        {
            Id = "input.cursorNavigation",
            Label = "Move around this window with the d-pad",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.InputMode.CursorNavigation,
            WriteFlag = Write(value => config.InputMode.CursorNavigation = value),
        },
        new SettingDefinition
        {
            Id = "input.contextMenu",
            Label = "Show Wayfarer in the game's menus",
            Description = "The only way to reach Wayfarer's actions without a cursor.",
            Kind = SettingKind.Choice,
            Options = ContextMenuLabels,
            ReadOption = () => (int)config.QuestHelper.MenuMode,
            WriteOption = Write(index => config.QuestHelper.MenuMode = (ContextMenuMode)index),
        },
        new SettingDefinition
        {
            Id = "window.position",
            Label = "Window position",
            Kind = SettingKind.Choice,
            Options = WindowPositionLabels,
            ReadOption = () => Array.IndexOf(WindowPositionValues, config.Hub.Position),
            WriteOption = index =>
            {
                config.Hub.Position = WindowPositionValues[index];
                saveConfig();
                OnWindowPositionChanged?.Invoke(config.Hub.Position);
            },
        },
    ];

    // Every setter saves. There is no "apply" button and there never should be — the game's own
    // configuration windows commit immediately too.
    private Action<bool> Write(Action<bool> apply) => value =>
    {
        apply(value);
        saveConfig();
    };

    private Action<int> Write(Action<int> apply) => value =>
    {
        apply(value);
        saveConfig();
    };

    private Action<float> WriteNumber(Action<float> apply) => value =>
    {
        apply(value);
        saveConfig();
    };
}
