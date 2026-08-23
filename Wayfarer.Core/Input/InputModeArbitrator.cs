namespace Wayfarer.Core.Input;

/// <summary>Pure state machine deciding the effective <see cref="InputMode"/> from a config
/// override, a startup seed and independently-tracked gamepad/mouse activity timestamps. No
/// Dalamud dependency — the plugin-side <c>InputModeService</c> is the only caller, and is
/// responsible for turning IGamepadState/ImGui IO/IGameConfig readings into the timestamps and
/// flags this takes. Fully unit-testable in isolation; see <see cref="Resolve"/>.</summary>
public static class InputModeArbitrator
{
    /// <summary>How much more recently the candidate device must have been active than the
    /// currently-active device before Auto mode switches to it. Measured between the two devices,
    /// not against the clock — which is why <see cref="Resolve"/> takes no current time. Prevents rapid flip-flopping
    /// from incidental cross-device noise — e.g. the mouse twitching once while the player is
    /// mid-controller-session, or analog stick drift while the mouse is the dominant device.
    /// Not applied on the very first switch away from a device that has never produced activity
    /// (nothing to debounce against yet — see <see cref="Resolve"/>).</summary>
    public static readonly TimeSpan Hysteresis = TimeSpan.FromSeconds(2);

    /// <summary>Resolves the effective <see cref="InputMode"/> for this frame.</summary>
    /// <param name="overrideMode">The config setting; <see cref="InputModeOverride.Mouse"/>/
    /// <see cref="InputModeOverride.Controller"/> short-circuit everything below.</param>
    /// <param name="seed">The mode to report before either device has ever produced activity —
    /// the plugin seeds this from the game's own <c>PadMode</c> config at startup.</param>
    /// <param name="previous">The mode this arbitrator returned last call, i.e. the
    /// currently-active mode that a candidate must unseat.</param>
    /// <param name="lastGamepadActivity">Timestamp of the most recent meaningful gamepad input
    /// (button press or past-deadzone stick movement), or <see langword="null"/> if none has
    /// been observed yet this session.</param>
    /// <param name="lastMouseActivity">Timestamp of the most recent mouse movement/click, or
    /// <see langword="null"/> if none has been observed yet this session. An exact tie against
    /// <paramref name="lastGamepadActivity"/> always leaves <paramref name="previous"/>
    /// unchanged: the internal candidate comparison below is strictly-greater-than for gamepad,
    /// so a tie computes Mouse as the candidate — but a tied gap is exactly zero, which never
    /// clears <see cref="Hysteresis"/>, so even when that computed candidate differs from
    /// <paramref name="previous"/> the switch is blocked and <paramref name="previous"/> is
    /// returned regardless of which mode it was. Net effect: an exact tie is a no-op either way.
    /// Pinned by <c>Auto_ExactTie_PreservesPrevious</c> (both previous-mode cases) in the test
    /// suite. Not expected to matter in practice — both timestamps come from separate
    /// <c>DateTimeOffset.UtcNow</c> reads in the same per-frame poll, so an exact tie needs
    /// clock-resolution luck to occur at all — but pinned rather than left implicit.</param>
    /// <param name="gamepadAvailable">The game's own <c>PadAvailable</c> flag. When false, Auto
    /// mode never resolves to <see cref="InputMode.Controller"/> — guards against stale activity
    /// timestamps outliving a controller that just got unplugged.</param>
    public static InputMode Resolve(
        InputModeOverride overrideMode,
        InputMode seed,
        InputMode previous,
        DateTimeOffset? lastGamepadActivity,
        DateTimeOffset? lastMouseActivity,
        bool gamepadAvailable)
    {
        switch (overrideMode)
        {
            case InputModeOverride.Mouse:
                return InputMode.Mouse;
            case InputModeOverride.Controller:
                return InputMode.Controller;
        }

        if (lastGamepadActivity is null && lastMouseActivity is null)
        {
            return gamepadAvailable ? seed : InputMode.Mouse;
        }

        var candidate = lastMouseActivity is null || lastGamepadActivity > lastMouseActivity
            ? InputMode.Controller
            : InputMode.Mouse;

        if (candidate == InputMode.Controller && !gamepadAvailable)
        {
            candidate = InputMode.Mouse;
        }

        if (candidate == previous)
        {
            return previous;
        }

        var previousLastActivity = previous == InputMode.Controller ? lastGamepadActivity : lastMouseActivity;
        var candidateLastActivity = candidate == InputMode.Controller ? lastGamepadActivity : lastMouseActivity;

        if (previousLastActivity is null || candidateLastActivity is null)
        {
            // The currently-active mode has never produced activity of its own (still riding the
            // startup seed, or the candidate is brand new) — nothing to debounce against, so the
            // first real signal wins outright.
            return candidate;
        }

        return candidateLastActivity.Value - previousLastActivity.Value >= Hysteresis ? candidate : previous;
    }
}
