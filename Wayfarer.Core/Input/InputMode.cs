namespace Wayfarer.Core.Input;

/// <summary>The effective input style the UI should present for right now — which device the
/// player most recently used (or the config override, when set). See
/// <see cref="InputModeArbitrator"/> for how this is resolved.</summary>
public enum InputMode
{
    Mouse,
    Controller,
}
