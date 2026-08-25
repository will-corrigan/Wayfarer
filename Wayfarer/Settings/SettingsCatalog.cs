using Wayfarer.Core.Input;
using Wayfarer.Core.Ui;
using Wayfarer.Modules;
using Wayfarer.Windows.Native;

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
internal sealed class SettingsCatalog(
    Configuration config, ModuleRegistry modules, ReadoutPlacement placement, Action saveConfig)
{
    private static readonly string[] InputModeLabels = ["Automatic", "Mouse and Keyboard", "Controller"];

    private static readonly InputModeOverride[] InputModeValues =
        [InputModeOverride.Auto, InputModeOverride.Mouse, InputModeOverride.Controller];

    // Top Centre first: it is the default, and it is the one placement on a stock 16:9 HUD that is
    // clear of both the minimap and the quest tracker. "Custom" is not offered as something to cycle
    // to — it is what the readout becomes once the player has actually moved it.
    private static readonly string[] ReadoutPositionLabels =
    [
        "Top Centre",
        "Top Left",
        "Top Right",
        "Bottom Left",
        "Bottom Centre",
        "Bottom Right",
        "Follow the Quest Tracker",
        "Where You Put It",
    ];

    private static readonly ReadoutPosition[] ReadoutPositionValues =
    [
        ReadoutPosition.TopCentre,
        ReadoutPosition.TopLeft,
        ReadoutPosition.TopRight,
        ReadoutPosition.BottomLeft,
        ReadoutPosition.BottomCentre,
        ReadoutPosition.BottomRight,
        ReadoutPosition.FollowQuestTracker,
        ReadoutPosition.Custom,
    ];

    // Plain colour names, in ArrowIconVariant's declaration order — the five are the same drawn
    // arrow in five colours, so the colour is the only thing there is to say about them.
    private static readonly string[] ArrowIconLabels = ["Amber", "Green", "Blue", "Red", "White"];

    private static readonly string[] WindowPositionLabels =
        ["Centre", "Top Left", "Top Right", "Bottom Left", "Bottom Right"];

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
        ["Quest in Progress", "Quest Available", "Main Scenario", "Look Here"];

    private static readonly int[] MarkerIconValues = [71223, 71221, 71203, 60094];

    // Positional, matching ContextMenuMode's declaration order.
    private static readonly string[] ContextMenuLabels = ["Never", "On a Controller", "Always"];

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
        new SettingSection("Readout Position", BuildReadoutPositionSettings()),
        new SettingSection("Controls", BuildControlSettings()),
    ];

    // A stored value that is no longer offered (a hand-edited config, or an option removed in a
    // later version) reads back as the first entry rather than as an out-of-range index.
    private static int IndexOf<T>(T[] values, T value)
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
            Label = "Mark the Target on the Map",
            Description = "Restores your own flag when the route ends.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.Guidance.MarkObjectiveWithMapFlag,
            WriteFlag = Write(value => config.Guidance.MarkObjectiveWithMapFlag = value),
        },
        new SettingDefinition
        {
            Id = "guidance.namePlates",
            Label = "Mark Targets Above Their Heads",
            Description = "Uses the game's own quest marker, and never replaces one the game put there.",
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
            Id = "questHelper.logDiagnostics",
            Label = "Log Readout Diagnostics",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.LogDiagnostics,
            WriteFlag = Write(value => config.QuestHelper.LogDiagnostics = value),
        },
        new SettingDefinition
        {
            Id = "guidance.clickTeleport",
            Label = "Click the Readout to Teleport",
            Description = "The only thing Wayfarer does that the server sees.",
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
            Label = "Show the Readout",
            Kind = SettingKind.Toggle,
            ReadFlag = () => !config.QuestHelper.WidgetHidden,
            WriteFlag = Write(value => config.QuestHelper.WidgetHidden = !value),
        },
        new SettingDefinition
        {
            Id = "readout.dtr",
            Label = "Show in the Server Info Bar",
            Description = "Left-click opens your unlocks, right-click opens settings. Always on screen, whatever the readout is set to.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => !config.QuestHelper.DtrHidden,
            WriteFlag = Write(value => config.QuestHelper.DtrHidden = !value),
        },
        new SettingDefinition
        {
            Id = "readout.textScale",
            Label = "Text Size",
            Kind = SettingKind.Scale,
            Minimum = 0.8f,
            Maximum = 2.0f,
            Step = 0.1f,
            ReadValue = () => config.QuestHelper.TextScale,
            WriteValue = WriteNumber(value => config.QuestHelper.TextScale = value),
        },
        new SettingDefinition
        {
            Id = "readout.arrowIcon",
            Label = "Arrow Colour",
            Description = "Applies at once.",
            Kind = SettingKind.Choice,
            Options = ArrowIconLabels,
            ReadOption = () => (int)config.QuestHelper.ArrowIcon,
            WriteOption = Write(index => config.QuestHelper.ArrowIcon = (ArrowIconVariant)index),
        },
        new SettingDefinition
        {
            Id = "readout.arrowScale",
            Label = "Arrow Size",
            Kind = SettingKind.Scale,
            Minimum = 0.5f,
            Maximum = 2.0f,
            Step = 0.1f,
            ReadValue = () => config.QuestHelper.ArrowScale,
            WriteValue = WriteNumber(value => config.QuestHelper.ArrowScale = value),
        },
    ];

    /// <summary>The readout's own position, as its own section — a preset to start from, two
    /// sliders that move it a step at a time, and the mouse's drag mode.
    ///
    /// <para><b>Why sliders and not a drag alone.</b> The player this was reported by is on a
    /// controller, with no cursor at all. These are <c>FloatSliderNode</c>s, which the game's own
    /// slider component steps with the d-pad, and every step writes and saves at once — so the
    /// readout moves live as they hold a direction, which is the only way to aim it without being
    /// able to see a cursor. They read back as a percentage of the usable screen rather than a pixel
    /// count, which is also how they survive a resolution change.</para></summary>
    private IReadOnlyList<SettingDefinition> BuildReadoutPositionSettings() =>
    [
        new SettingDefinition
        {
            Id = "readout.position",
            Label = "Position",
            Description = "A starting point. Nudge or drag it and it stays where you leave it.",
            Kind = SettingKind.Choice,
            Options = ReadoutPositionLabels,
            ReadOption = () => IndexOf(ReadoutPositionValues, config.QuestHelper.ReadoutPosition),
            WriteOption = Write(index => config.QuestHelper.ReadoutPosition = ReadoutPositionValues[index]),
        },
        new SettingDefinition
        {
            Id = "readout.positionX",
            Label = "Across the Screen",
            Description = "0% is hard left, 100% is hard right.",
            Kind = SettingKind.Scale,
            Minimum = 0f,
            Maximum = 100f,
            Step = 1f,
            ValueFormat = "0",
            ValueUnit = "%",
            ReadValue = () => placement.FractionX * 100f,
            WriteValue = value => placement.SetFractionX(value / 100f),
        },
        new SettingDefinition
        {
            Id = "readout.positionY",
            Label = "Down the Screen",
            Description = "0% is hard top, 100% is hard bottom.",
            Kind = SettingKind.Scale,
            Minimum = 0f,
            Maximum = 100f,
            Step = 1f,
            ValueFormat = "0",
            ValueUnit = "%",
            ReadValue = () => placement.FractionY * 100f,
            WriteValue = value => placement.SetFractionY(value / 100f),
        },
        new SettingDefinition
        {
            Id = "readout.moveMode",
            Label = "Move the Readout with the Mouse",
            Description = "Puts a drag handle on the readout. While it is on, clicks on the readout move it instead of reaching the world.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.ReadoutMoveMode,
            WriteFlag = Write(value => config.QuestHelper.ReadoutMoveMode = value),
        },
    ];

    private IReadOnlyList<SettingDefinition> BuildReadoutContent() =>
    [
        new SettingDefinition
        {
            Id = "readout.unlocks",
            Label = "Show Nearby Unlocks",
            Description = "Adds the nearest few, with distances, as extra lines under the readout. Off to start "
                + "with; the unlocks tab lists them all either way.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.UnlockChecklist.ShowOnWidget,
            WriteFlag = Write(value => config.UnlockChecklist.ShowOnWidget = value),
        },
        new SettingDefinition
        {
            Id = "readout.hunting",
            Label = "Show Hunting Progress",
            Description = "Adds a line about a hunt the arrow is not currently following.  The "
                + "hunting tab has the whole log.",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.HuntingLog.ShowOnWidget,
            WriteFlag = Write(value => config.HuntingLog.ShowOnWidget = value),
        },
        new SettingDefinition
        {
            Id = "readout.hideInCombat",
            Label = "Hide in Combat",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.ArrowHideInCombat,
            WriteFlag = Write(value => config.QuestHelper.ArrowHideInCombat = value),
        },
        new SettingDefinition
        {
            Id = "readout.hideInDuty",
            Label = "Hide in Duties",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.QuestHelper.ArrowHideInDuty,
            WriteFlag = Write(value => config.QuestHelper.ArrowHideInDuty = value),
        },
        new SettingDefinition
        {
            Id = "readout.native",
            Label = "Use the Game's Own Text",
            Description = "Turn off to fall back to the older widget.",
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
            ReadOption = () => IndexOf(InputModeValues, config.InputMode.Override),
            WriteOption = Write(index => config.InputMode.Override = InputModeValues[index]),
        },
        new SettingDefinition
        {
            Id = "input.cursorNavigation",
            Label = "Move with the D-Pad",
            Kind = SettingKind.Toggle,
            ReadFlag = () => config.InputMode.CursorNavigation,
            WriteFlag = Write(value => config.InputMode.CursorNavigation = value),
        },
        new SettingDefinition
        {
            Id = "input.contextMenu",
            Label = "Show in the Game's Menus",
            Description = "Reaches Wayfarer's actions without a cursor.",
            Kind = SettingKind.Choice,
            Options = ContextMenuLabels,
            ReadOption = () => (int)config.QuestHelper.MenuMode,
            WriteOption = Write(index => config.QuestHelper.MenuMode = (ContextMenuMode)index),
        },
        new SettingDefinition
        {
            Id = "window.position",
            Label = "Window Position",
            Kind = SettingKind.Choice,
            Options = WindowPositionLabels,
            ReadOption = () => IndexOf(WindowPositionValues, config.Hub.Position),
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
