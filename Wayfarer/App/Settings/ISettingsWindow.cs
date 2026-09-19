namespace Wayfarer.App.Settings;

/// <summary>The settings window, for the surfaces that offer a way into it.</summary>
internal interface ISettingsWindow
{
    /// <summary>Opens the window, or closes it when it is already open. Safe to call from
    /// anywhere; the window is opened on the thread the game needs.</summary>
    void Toggle();
}
