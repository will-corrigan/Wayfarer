using Wayfarer.App.Config;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The one copy of the block's style: loaded once, changed by the settings window, and
/// announced to the surface, which re-lays the block on its next update.</summary>
internal sealed class ScenarioTreeStyleStore(IConfigStore configs)
{
    private const string ConfigName = "scenario-tree";

    /// <summary>Raised after <see cref="Current"/> changed, on whatever thread changed it.</summary>
    public event EventHandler? OnChanged;

    /// <summary>The style in force. Always clamped.</summary>
    public ScenarioTreeStyle Current { get; private set; } = configs.Load<ScenarioTreeStyle>(ConfigName).Clamped();

    /// <summary>Makes a style current, clamped, saves it, and tells the listeners.</summary>
    public void Apply(ScenarioTreeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        Current = style.Clamped();
        configs.Save(ConfigName, Current);
        OnChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Back to the defaults.</summary>
    public void Reset() => Apply(new ScenarioTreeStyle());
}
