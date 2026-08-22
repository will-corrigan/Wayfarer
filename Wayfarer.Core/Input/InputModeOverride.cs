namespace Wayfarer.Core.Input;

/// <summary>User-facing config setting: <see cref="Auto"/> lets <see cref="InputModeArbitrator"/>
/// pick <see cref="InputMode"/> from device activity; <see cref="Mouse"/>/<see cref="Controller"/>
/// pin it regardless of what the player's hands are doing.</summary>
public enum InputModeOverride
{
    Auto,
    Mouse,
    Controller,
}
