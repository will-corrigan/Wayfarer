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
/// <param name="IsFollowed">Whether this is what is being followed right now. Exactly one entry is
/// true at any moment, and never zero: not following anything in particular is what following the
/// main scenario means.
///
/// <para>Derived from the source that actually holds the arrow, never from the followed-quest
/// override alone — that override is null during a hunt and during an unlock route, so reading it
/// as "following the main scenario" made this list claim the main scenario was being followed in
/// the middle of a hunt, and left the two engaged kinds unable to report themselves at
/// all.</para></param>
/// <param name="Activate">What picking this entry does. <b>Non-null whenever the feature behind it
/// exists at all</b>, even with nothing to route to right now: it then opens that feature's own page,
/// which says why. An entry that is listed, focusable and inert is a press that does nothing, and
/// that is exactly what this list is not allowed to contain — null here is reserved for the case
/// where the feature is switched off and there is nothing to open either.</para></param>
/// <param name="Ready">Whether picking it starts something right now — a routable unlock, a target
/// left on the rank. What the tab's rows colour themselves from, and what their detail pane offers a
/// button for; the entry is still listed and still acts when this is false, because a choice that
/// vanishes when it is empty cannot be learned.</param>
internal readonly record struct FollowChoice(
    string Label, string Detail, bool IsFollowed, Action? Activate, bool Ready);
