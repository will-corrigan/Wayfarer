using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;

namespace Wayfarer;

/// <summary>Adaptive input-mode detection consumed by every window/module that adapts its
/// presentation to Mouse vs Controller. Feeds the pure <see cref="InputModeArbitrator"/> from
/// <see cref="IGamepadState"/> activity, ImGui's per-frame mouse delta, and the game's own
/// PadMode/PadAvailable/PadReverseConfirmCancel config (<see cref="IGameConfig"/>). Polled once
/// per ImGui frame via <see cref="OnFrame"/> — the plugin composition root subscribes it to
/// <c>UiBuilder.Draw</c> ahead of the window system so every window sees the current-frame mode.
/// Raises <see cref="OnModeChanged"/> when the resolved mode flips.</summary>
internal sealed class InputModeService
{
    // Analog stick magnitude below which movement is treated as drift/noise, not activity.
    private const float StickDeadzone = 0.2f;

    // Every physical button IGamepadState tracks; checked as a whole for "any button pressed"
    // activity (raw magnitude, not just Pressed(), so held analog triggers still count).
    private static readonly GamepadButtons[] AllButtons =
    [
        GamepadButtons.DpadUp, GamepadButtons.DpadDown, GamepadButtons.DpadLeft, GamepadButtons.DpadRight,
        GamepadButtons.North, GamepadButtons.South, GamepadButtons.West, GamepadButtons.East,
        GamepadButtons.L1, GamepadButtons.L2, GamepadButtons.L3,
        GamepadButtons.R1, GamepadButtons.R2, GamepadButtons.R3,
        GamepadButtons.Select, GamepadButtons.Start,
    ];

    private readonly IGameConfig gameConfig;
    private readonly IGamepadState gamepadState;
    private readonly InputModeConfig cfg;
    private readonly IPluginLog log;
    private readonly InputMode seed;

    // One entry per option that has already been complained about. OnFrame reads two options every
    // ImGui frame, so an option that throws once throws sixty times a second: without this the log
    // would be nothing but this one line.
    private readonly HashSet<SystemConfigOption> unreadableOptions = [];

    private DateTimeOffset? lastGamepadActivity;
    private DateTimeOffset? lastMouseActivity;

    public InputModeService(IGameConfig gameConfig, IGamepadState gamepadState, InputModeConfig cfg, IPluginLog log)
    {
        this.gameConfig = gameConfig;
        this.gamepadState = gamepadState;
        this.cfg = cfg;
        this.log = log;

        seed = ReadUintAsBool(SystemConfigOption.PadMode) ? InputMode.Controller : InputMode.Mouse;
        Mode = seed;
        ReverseConfirmCancel = ReadUintAsBool(SystemConfigOption.PadReverseConfirmCancel);
    }

    public event Action<InputMode>? OnModeChanged;

    public InputMode Mode { get; private set; }

    /// <summary>The player's own confirm/cancel orientation. This is the only pad setting Wayfarer
    /// still reads for presentation: <b>which</b> physical button confirms is genuinely a separate
    /// question from what that button looks like, and the game answers the second one itself by
    /// swapping the glyph atlas behind <c>BitmapFontIcon.ControllerButton0..3</c>.
    ///
    /// The previous attempt to read the pad's <i>family</i> from PadSelectButtonIcon is gone. That
    /// option is not a two-value flag — the game's own settings list it as seven entries with the
    /// Xbox layouts first — so treating raw 0 as PlayStation showed ✕ and ○ to a player holding a
    /// default Xbox pad.</summary>
    public bool ReverseConfirmCancel { get; private set; }

    /// <summary>Polled once per ImGui frame. Safe to call every frame regardless of whether any
    /// window is open — IGamepadState/IGameConfig reads are cheap, and ImGui.GetIO() is only
    /// valid from within a Draw callback, which is why this isn't a Framework.Update hook.</summary>
    public void OnFrame()
    {
        var now = DateTimeOffset.UtcNow;

        if (HasGamepadActivity())
        {
            lastGamepadActivity = now;
        }

        if (HasMouseActivity())
        {
            lastMouseActivity = now;
        }

        var available = ReadUintAsBool(SystemConfigOption.PadAvailable, defaultValue: true);
        ReverseConfirmCancel = ReadUintAsBool(SystemConfigOption.PadReverseConfirmCancel);

        var resolved = InputModeArbitrator.Resolve(
            cfg.Override, seed, Mode, lastGamepadActivity, lastMouseActivity, available, now);

        if (resolved != Mode)
        {
            Mode = resolved;
            OnModeChanged?.Invoke(Mode);
        }
    }

    private static bool HasMouseActivity()
    {
        var io = ImGui.GetIO();
        return io.MouseDelta != Vector2.Zero
            || ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            || ImGui.IsMouseClicked(ImGuiMouseButton.Right);
    }

    private bool HasGamepadActivity()
    {
        foreach (var button in AllButtons)
        {
            if (gamepadState.Raw(button) != 0f)
            {
                return true;
            }
        }

        return gamepadState.LeftStick.LengthSquared() > StickDeadzone * StickDeadzone
            || gamepadState.RightStick.LengthSquared() > StickDeadzone * StickDeadzone;
    }

    /// <summary>Every pad-related SystemConfigOption this service reads is fetched via the uint
    /// overload of TryGet (verified by reflection over IGameConfig) rather than the bool overload
    /// — TryGet returns false (not a throw) on a type mismatch, so this degrades to
    /// <paramref name="defaultValue"/> harmlessly if a future Dalamud/game update changes the
    /// underlying representation.</summary>
    private bool ReadUintAsBool(SystemConfigOption option, bool defaultValue = false)
    {
        try
        {
            return gameConfig.TryGet(option, out uint raw) ? raw != 0 : defaultValue;
        }
        catch (Exception ex)
        {
            if (unreadableOptions.Add(option))
            {
                var message =
                    $"Wayfarer input: the game's {option} setting could not be read, so Wayfarer is assuming " +
                    $"{defaultValue} for the rest of the session. Controller detection may be wrong; set Input " +
                    "explicitly in Wayfarer's settings if it is. Not reported again.";
                log.Warning(ex, message);
            }

            return defaultValue;
        }
    }
}
