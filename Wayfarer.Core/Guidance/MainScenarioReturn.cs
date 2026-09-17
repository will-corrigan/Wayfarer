namespace Wayfarer.Core.Guidance;

/// <summary>Which of Wayfarer's follow modes is running right now. There is no "nothing": following
/// nothing in particular IS <see cref="FollowMode.MainScenario"/>, which is why that member is the
/// fallback rather than a null.</summary>
public enum FollowMode
{
    /// <summary>The default loop — the main scenario, or the ambient objective when there is no
    /// explicit selection at all.</summary>
    MainScenario,

    /// <summary>A particular accepted quest, chosen by the player.</summary>
    Quest,
}

/// <summary>What returning to the main scenario has to <b>do</b> from where the player is now — not
/// whether an entry for it exists, but which operations that entry must perform to have any effect.
///
/// <para>Both flags matter independently, and that is the whole point of the type: an engaged mode
/// owns the arrow while a followed quest can be set underneath it, so a "Main Scenario" that only
/// cleared the followed quest would leave the engaged source running, and one that only released the
/// engaged source would drop the player back onto a side quest.</para></summary>
/// <param name="ReleaseEngagedSource">The engaged source has to be released.</param>
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
/// by every surface that offers it.
///
/// <para><b>Why this is not a bool at each call site.</b> Each of those surfaces used to decide for
/// itself, and some decided it from the followed-quest override alone — so with a source engaged they
/// concluded that the main scenario was already being followed, greyed the entry out and left a
/// controller player with every route home disabled. The condition is one condition; it is written
/// here once, and the surfaces render it.</para></summary>
public static class MainScenarioReturn
{
    /// <summary>Which mode the player is in: a particular accepted quest if one has been chosen, and
    /// otherwise the default loop.</summary>
    /// <param name="hasFollowedQuest">Whether a particular accepted quest is being followed.</param>
    public static FollowMode ModeOf(bool hasFollowedQuest) =>
        hasFollowedQuest ? FollowMode.Quest : FollowMode.MainScenario;

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
