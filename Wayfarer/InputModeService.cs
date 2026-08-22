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
        IsPlayStationPad = ReadIsPlayStationPad();
        Glyphs = GlyphSet.For(IsPlayStationPad, ReadUintAsBool(SystemConfigOption.PadReverseConfirmCancel));
    }

    public event Action<InputMode>? OnModeChanged;

    public InputMode Mode { get; private set; }

    public GlyphSet Glyphs { get; private set; }

    /// <summary>Whether the game's <c>PadSelectButtonIcon</c> setting is confirmed PlayStation
    /// (raw value 0). False for Xbox (raw value 1) AND for any unread/unrecognized value — see
    /// <see cref="GlyphSet"/>'s doc comment for why "unknown" defaults to text labels rather than a
    /// possibly-wrong glyph shape.</summary>
    public bool IsPlayStationPad { get; private set; }

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
        IsPlayStationPad = ReadIsPlayStationPad();
        Glyphs = GlyphSet.For(IsPlayStationPad, ReadUintAsBool(SystemConfigOption.PadReverseConfirmCancel));

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
            log.Warning(ex, $"InputModeService: failed reading {option}");
            return defaultValue;
        }
    }

    /// <summary>PadSelectButtonIcon is 0 for PlayStation, 1 for Xbox (community-documented values —
    /// no Dalamud/Lumina enum exists for this option). Only raw value 0 is treated as confirmed
    /// PlayStation; a read failure or any other value (including a possible future console's icon
    /// set) falls back to text labels via <see cref="GlyphSet"/>, never a guessed glyph shape.</summary>
    private bool ReadIsPlayStationPad()
    {
        try
        {
            return gameConfig.TryGet(SystemConfigOption.PadSelectButtonIcon, out uint raw) && raw == 0;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "InputModeService: failed reading PadSelectButtonIcon");
            return false;
        }
    }
}
