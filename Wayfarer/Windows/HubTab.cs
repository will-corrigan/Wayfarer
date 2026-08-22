namespace Wayfarer.Windows;

/// <summary>Which of the <see cref="NativeHubWindow"/>'s three tabs is currently shown.
/// <see cref="Checklist"/> and <see cref="Hunting"/> match <see cref="Modules.UnlockChecklistModule"/>/
/// <see cref="Modules.HuntingLogModule"/>'s data; <see cref="Settings"/> hosts the
/// controller-navigable subset of <see cref="ConfigWindow"/>.</summary>
internal enum HubTab
{
    Checklist,
    Hunting,
    Settings,
}
