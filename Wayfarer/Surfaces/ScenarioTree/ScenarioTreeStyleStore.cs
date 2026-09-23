using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Wayfarer.App.Config;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The one copy of the block's style: loaded once, changed by the settings window, and
/// announced to the surface, which re-lays the block on its next update.
///
/// <para>The style is current the moment it is applied, so the preview follows a slider as it
/// moves; the file is written a little after the slider stops. A slider raises a change for every
/// step it is dragged through, and each of those was a write to disk on the game's own thread.</para>
/// </summary>
internal sealed class ScenarioTreeStyleStore : IDisposable
{
    private const string ConfigName = "scenario-tree";

    /// <summary>How long after the last change the file is written. Long enough to outlast a drag,
    /// short enough that a plugin unloaded straight after a change still has it saved.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly IConfigStore configs;
    private readonly IDebouncer save;

    public ScenarioTreeStyleStore(IConfigStore configs, IFramework framework)
    {
        this.configs = configs;
        Current = configs.Load<ScenarioTreeStyle>(ConfigName).Clamped();
        save = framework.CreateDebouncer(SaveDelay, () => configs.Save(ConfigName, Current));
    }

    /// <summary>Raised after <see cref="Current"/> changed, on whatever thread changed it.</summary>
    public event EventHandler? OnChanged;

    /// <summary>The style in force. Always clamped.</summary>
    public ScenarioTreeStyle Current { get; private set; }

    /// <summary>Makes a style current, clamped, tells the listeners, and has it saved once the
    /// changes stop coming.</summary>
    public void Apply(ScenarioTreeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        Current = style.Clamped();
        save.Debounce();
        OnChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Back to the defaults.</summary>
    public void Reset() => Apply(new ScenarioTreeStyle());

    /// <summary>Writes a change still waiting to be saved, so nothing the player set is lost to
    /// the unload arriving first.</summary>
    public void Dispose()
    {
        if (save.IsPending)
        {
            save.Cancel();
            configs.Save(ConfigName, Current);
        }

        save.Dispose();
    }
}
