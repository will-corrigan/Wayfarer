namespace Wayfarer.Core.Guidance;

/// <summary>Which of Wayfarer's four follow modes is running right now. There is no fifth, and there
/// is no "nothing": following nothing in particular IS
/// <see cref="FollowMode.MainScenario"/>, which is why that member is the fallback rather than a
/// null.</summary>
public enum FollowMode
{
    /// <summary>The default loop — the main scenario, or the ambient objective when there is no
    /// explicit selection at all.</summary>
    MainScenario,

    /// <summary>A particular accepted quest, chosen by the player.</summary>
    Quest,

    /// <summary>An engaged route through the unlock checklist.</summary>
    UnlockRoute,

    /// <summary>An engaged hunt through the hunting log's remaining targets.</summary>
    Hunting,
}

/// <summary>What returning to the main scenario has to <b>do</b> from where the player is now — not
/// whether an entry for it exists, but which operations that entry must perform to have any effect.
///
/// <para>Both flags matter independently, and that is the whole point of the type: an engaged mode
/// owns the arrow while a followed quest can be set underneath it, so a "Main Scenario" that only
/// cleared the followed quest would leave a hunt running, and one that only released the engaged
/// source would drop the player back onto a side quest.</para></summary>
/// <param name="ReleaseEngagedSource">The engaged source (a hunt, an unlock route) has to be
/// released.</param>
/// <param name="ClearFollowedQuest">The followed-quest override has to be cleared.</param>
public readonly record struct FollowReset(bool ReleaseEngagedSource, bool ClearFollowedQuest)
{
    /// <summary>Whether performing this reset changes anything. A control offered while this is
    /// false is a control that would accept a press and do nothing — which is the defect this type
    /// exists to make impossible to reintroduce, since it is the same condition that decides whether
    /// the control is offered at all.</summary>
    public bool Acts => ReleaseEngagedSource || ClearFollowedQuest;
}

/// <summary>The one answer to "how does the player get back to the Main Scenario from here", shared
/// by every surface that offers it: the readout's follow switcher, the readout's own subcommand
/// menu, the game's right-click menu and the hub window's Following tab.
///
/// <para><b>Why this is not a bool at each call site.</b> Each of those surfaces used to decide for
/// itself, and two of them decided it from the followed-quest override alone — so during a hunt they
/// concluded that the main scenario was already being followed, greyed the entry out and left a
/// controller player with a readout whose every route home was disabled. The condition is one
/// condition; it is written here once, and the surfaces render it.</para></summary>
public static class MainScenarioReturn
{
    /// <summary>Which mode the player is in, from the two facts that decide it: what is engaged, and
    /// whether a quest has been chosen. An engaged mode wins — it is what the readout is describing —
    /// and anything else with no chosen quest is the default loop.
    ///
    /// <para>The engaged mode is passed IN rather than resolved from a source id here. Mapping ids to
    /// features is the plugin's business: nothing under <c>Wayfarer.Core/Guidance</c> may know what a
    /// particular source is called, which is the rule <c>CoreGuidanceIsolationTests</c> enforces and
    /// the reason a hunting target was substitutable at all. The mapping lives on the navigator, which
    /// asks each source for its own id rather than writing one down.</para></summary>
    /// <param name="engaged">The mode the engaged source stands for, or null when nothing is
    /// engaged.</param>
    /// <param name="hasFollowedQuest">Whether a particular accepted quest is being followed.</param>
    public static FollowMode ModeOf(FollowMode? engaged, bool hasFollowedQuest) =>
        engaged ?? (hasFollowedQuest ? FollowMode.Quest : FollowMode.MainScenario);

    /// <summary>What a "Main Scenario" control must do from this state.</summary>
    /// <param name="engaged">An explicit mode owns the arrow right now.</param>
    /// <param name="hasFollowedQuest">A particular accepted quest is being followed.</param>
    public static FollowReset From(bool engaged, bool hasFollowedQuest) => new(engaged, hasFollowedQuest);

    /// <summary>Whether the player is already on the main scenario, and therefore whether the
    /// control that returns them to it has nothing to do. The inverse of
    /// <see cref="FollowReset.Acts"/>, spelled out because that is the word the surfaces use — an
    /// entry marked "(following)" and an entry that is disabled must be the same entry.</summary>
    /// <inheritdoc cref="From"/>
    public static bool AlreadyThere(bool engaged, bool hasFollowedQuest) =>
        !From(engaged, hasFollowedQuest).Acts;
}
