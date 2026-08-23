using Wayfarer.Core.Input;

namespace Wayfarer.Tests;

public class InputModeArbitratorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Override_Mouse_AlwaysWinsRegardlessOfActivity()
    {
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Mouse,
            seed: InputMode.Controller,
            previous: InputMode.Controller,
            lastGamepadActivity: T0,
            lastMouseActivity: null,
            gamepadAvailable: true);

        Assert.Equal(InputMode.Mouse, result);
    }

    [Fact]
    public void Override_Controller_AlwaysWinsRegardlessOfActivity()
    {
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Controller,
            seed: InputMode.Mouse,
            previous: InputMode.Mouse,
            lastGamepadActivity: null,
            lastMouseActivity: T0,
            gamepadAvailable: false);

        Assert.Equal(InputMode.Controller, result);
    }

    [Theory]
    [InlineData(InputMode.Mouse)]
    [InlineData(InputMode.Controller)]
    public void Auto_NoActivityEver_ReturnsSeed(InputMode seed)
    {
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed,
            previous: seed,
            lastGamepadActivity: null,
            lastMouseActivity: null,
            gamepadAvailable: true);

        Assert.Equal(seed, result);
    }

    [Fact]
    public void Auto_NoActivityEver_GamepadUnavailable_SeedIgnored_ReturnsMouse()
    {
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Controller,
            previous: InputMode.Controller,
            lastGamepadActivity: null,
            lastMouseActivity: null,
            gamepadAvailable: false);

        Assert.Equal(InputMode.Mouse, result);
    }

    [Fact]
    public void Auto_FirstEverGamepadActivity_SwitchesImmediately_NoDebounce()
    {
        // previous=Mouse has no recorded mouse activity of its own (still on the startup seed) —
        // nothing to debounce against, so the very first controller touch wins outright.
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Mouse,
            previous: InputMode.Mouse,
            lastGamepadActivity: T0.AddMilliseconds(1),
            lastMouseActivity: null,
            gamepadAvailable: true);

        Assert.Equal(InputMode.Controller, result);
    }

    [Fact]
    public void Auto_FirstEverMouseActivity_SwitchesImmediately_NoDebounce()
    {
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Controller,
            previous: InputMode.Controller,
            lastGamepadActivity: null,
            lastMouseActivity: T0.AddMilliseconds(1),
            gamepadAvailable: true);

        Assert.Equal(InputMode.Mouse, result);
    }

    [Fact]
    public void Auto_CandidateMoreRecentByLessThanHysteresis_StaysOnPrevious()
    {
        var mouseAt = T0;
        var gamepadAt = T0 + TimeSpan.FromSeconds(1); // < 2s hysteresis ahead of the mouse

        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Mouse,
            previous: InputMode.Mouse,
            lastGamepadActivity: gamepadAt,
            lastMouseActivity: mouseAt,
            gamepadAvailable: true);

        Assert.Equal(InputMode.Mouse, result);
    }

    [Fact]
    public void Auto_CandidateMoreRecentByAtLeastHysteresis_Switches()
    {
        var mouseAt = T0;
        var gamepadAt = T0 + InputModeArbitrator.Hysteresis;

        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Mouse,
            previous: InputMode.Mouse,
            lastGamepadActivity: gamepadAt,
            lastMouseActivity: mouseAt,
            gamepadAvailable: true);

        Assert.Equal(InputMode.Controller, result);
    }

    [Theory]
    [InlineData(InputMode.Mouse)]
    [InlineData(InputMode.Controller)]
    public void Auto_ExactTie_PreservesPrevious(InputMode previous)
    {
        // Both devices report the identical timestamp. The internal candidate comparison is
        // strictly-greater-than for gamepad, so a tie computes Mouse as the candidate - but a
        // tied gap is exactly zero, which never clears Hysteresis, so the switch is blocked
        // regardless of which mode was previously active: an exact tie is always a no-op.
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Mouse,
            previous,
            lastGamepadActivity: T0,
            lastMouseActivity: T0,
            gamepadAvailable: true);

        Assert.Equal(previous, result);
    }

    [Fact]
    public void Auto_CandidateSameAsPrevious_NoOp()
    {
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Mouse,
            previous: InputMode.Controller,
            lastGamepadActivity: T0 + TimeSpan.FromSeconds(5),
            lastMouseActivity: T0,
            gamepadAvailable: true);

        Assert.Equal(InputMode.Controller, result);
    }

    [Fact]
    public void Auto_GamepadCandidateButUnavailable_FallsBackToMouse()
    {
        // Stale gamepad timestamp outlives a controller that just got unplugged.
        var result = InputModeArbitrator.Resolve(
            InputModeOverride.Auto,
            seed: InputMode.Mouse,
            previous: InputMode.Mouse,
            lastGamepadActivity: T0 + TimeSpan.FromSeconds(10),
            lastMouseActivity: T0,
            gamepadAvailable: false);

        Assert.Equal(InputMode.Mouse, result);
    }

    [Fact]
    public void Auto_SustainedMouseUseSuppressesGamepadNoise()
    {
        // Both devices keep producing near-simultaneous activity (e.g. stick drift while the
        // player is actively mousing) — the gap never reaches the hysteresis window, so the
        // mode never flips.
        var previous = InputMode.Mouse;
        for (var i = 0; i < 5; i++)
        {
            var t = T0 + TimeSpan.FromSeconds(i);
            previous = InputModeArbitrator.Resolve(
                InputModeOverride.Auto,
                seed: InputMode.Mouse,
                previous,
                lastGamepadActivity: t,
                lastMouseActivity: t,
                gamepadAvailable: true);
        }

        Assert.Equal(InputMode.Mouse, previous);
    }
}
