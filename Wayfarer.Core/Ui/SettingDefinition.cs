using System.Globalization;

namespace Wayfarer.Core.Ui;

/// <summary>One setting, declared once and rendered by every presentation.
///
/// This is the abstraction half of the bridge: the settings live here, as data, with a getter and a
/// setter each; the native window and the ImGui fallback are two implementations that know how to
/// draw a <see cref="SettingKind"/> and nothing about what any particular setting means. Before
/// this existed the same handful of settings were written out twice — once as
/// <c>ImGui.Checkbox</c> calls inside each module and once as <c>CheckboxNode</c>s inside the hub —
/// which is how the controller player ended up with no reachable way to turn off a widget line.
///
/// Adding a setting is one entry in the catalog; both presentations pick it up with no further
/// work. Removing one is the same in reverse. Neither presentation may add a setting of its own.</summary>
public sealed class SettingDefinition
{
    /// <summary>Stable identifier, used for logging and for tests — never shown to the player.</summary>
    public required string Id { get; init; }

    /// <summary>The player-facing label, in Title Case — the game titles its own settings, tabs and
    /// headings that way, and a plugin sitting beside them that does not looks like a plugin.
    /// Descriptions are the exception and stay in sentence case, because they are sentences.</summary>
    public required string Label { get; init; }

    /// <summary>One line of explanation, shown under the control where a presentation has room.</summary>
    public string? Description { get; init; }

    public required SettingKind Kind { get; init; }

    /// <summary><see cref="SettingKind.Toggle"/> only.</summary>
    public Func<bool>? ReadFlag { get; init; }

    /// <summary><see cref="SettingKind.Toggle"/> only.</summary>
    public Action<bool>? WriteFlag { get; init; }

    /// <summary><see cref="SettingKind.Choice"/> only — the option labels, in order.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary><see cref="SettingKind.Choice"/> only — the selected index.</summary>
    public Func<int>? ReadOption { get; init; }

    /// <summary><see cref="SettingKind.Choice"/> only.</summary>
    public Action<int>? WriteOption { get; init; }

    /// <summary><see cref="SettingKind.Scale"/> only.</summary>
    public float Minimum { get; init; }

    /// <summary><see cref="SettingKind.Scale"/> only.</summary>
    public float Maximum { get; init; } = 1f;

    /// <summary><see cref="SettingKind.Scale"/> only.</summary>
    public float Step { get; init; } = 0.1f;

    /// <summary><see cref="SettingKind.Scale"/> only.</summary>
    public Func<float>? ReadValue { get; init; }

    /// <summary><see cref="SettingKind.Scale"/> only.</summary>
    public Action<float>? WriteValue { get; init; }

    /// <summary><see cref="SettingKind.Scale"/> only — how the value reads to the player. Defaults
    /// to the multiplier form ("1.2x") the size settings use; the readout's position sliders are
    /// percentages of the screen, which is a different thing entirely and has to say so.</summary>
    public string ValueFormat { get; init; } = "0.0";

    /// <summary><see cref="SettingKind.Scale"/> only — the unit appended to
    /// <see cref="ValueFormat"/>.</summary>
    public string ValueUnit { get; init; } = "x";

    /// <summary>The current value as the player would read it. Shared by both presentations so a
    /// setting reads identically wherever it is shown — the native cycle button's label and the
    /// ImGui row's trailing text are the same string.</summary>
    public string CurrentValueText() => Kind switch
    {
        SettingKind.Toggle => ReadFlag?.Invoke() == true ? "On" : "Off",
        SettingKind.Choice => OptionLabel(ReadOption?.Invoke() ?? 0),
        SettingKind.Scale => (ReadValue?.Invoke() ?? 0f).ToString(ValueFormat, CultureInfo.CurrentCulture) + ValueUnit,
        _ => string.Empty,
    };

    /// <summary>Advances a choice to the next option, wrapping — what a controller confirm does on
    /// a cycle button, and the only way to change a choice without a popup the cursor has to be
    /// taught to reach.</summary>
    public void CycleOption()
    {
        if (Kind != SettingKind.Choice || Options.Count == 0 || ReadOption is null || WriteOption is null)
        {
            return;
        }

        WriteOption((ReadOption() + 1) % Options.Count);
    }

    /// <summary>Flips a toggle.</summary>
    public void Toggle()
    {
        if (Kind == SettingKind.Toggle && ReadFlag is not null && WriteFlag is not null)
        {
            WriteFlag(!ReadFlag());
        }
    }

    private string OptionLabel(int index) =>
        index >= 0 && index < Options.Count ? Options[index] : string.Empty;
}
