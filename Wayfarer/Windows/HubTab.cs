namespace Wayfarer.Windows;

/// <summary>Which of the <see cref="NativeHubWindow"/>'s tabs is currently shown.
/// <see cref="Checklist"/> and <see cref="Hunting"/> match <see cref="Modules.UnlockChecklistModule"/>/
/// <see cref="Modules.HuntingLogModule"/>'s data; <see cref="Quests"/> is where what Wayfarer follows
/// is chosen and where the readout's guidance gets the buttons an overlay cannot carry;
/// <see cref="Settings"/> hosts the controller-navigable subset of <see cref="ConfigWindow"/>.
///
/// <para><b>The member names are not the labels.</b> <see cref="Quests"/> is drawn as "Following"
/// and is the leftmost tab and the one the window opens on; <see cref="Checklist"/> is drawn as
/// "Unlocks". The names are kept because they are what every call site already says, and renaming
/// an enum to fix a word on screen changes a great deal of code to change nothing a player can see.
/// <c>NativeHubWindow.TabLabel</c> is the single place the labels live.</para></summary>
internal enum HubTab
{
    Checklist,
    Hunting,
    Quests,
    Settings,
}
