namespace Wayfarer.Windows.Native;

/// <summary>One thing Wayfarer can be told to follow — the shape the Following tab's rows and the
/// readout's switcher dropdown both build from, so the two surfaces can never list different
/// choices. See <see cref="NativeHubWindow.GetFollowChoices"/>, the one place this list is
/// computed.</summary>
/// <param name="Label">What the entry is called — the same word the tab and the game's own Follow
/// context-menu use.</param>
/// <param name="Detail">A short trailing caption — "Following", a routable count, an accepted-quest
/// count — or empty. The same word the tab's own right-hand column shows for this row, so a count
/// cannot drift between the two surfaces.</param>
/// <param name="IsFollowed">Whether this is what is being followed right now. At most one entry is
/// ever true, and never zero: not following anything in particular is what following the main
/// scenario means.</param>
/// <param name="Activate">What picking this entry does, or null when there is nothing to activate
/// right now (an unlock route with nothing routable, a hunting log with nothing left) — the entry is
/// still listed, inert, because a choice that vanishes when it is empty cannot be learned.</param>
internal readonly record struct FollowChoice(string Label, string Detail, bool IsFollowed, Action? Activate);
