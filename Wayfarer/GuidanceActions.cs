using Dalamud.Plugin.Services;
using Wayfarer.Core.Navigation;
using Wayfarer.Modules;

namespace Wayfarer;

/// <summary>Every action the game's right-click menu offers, in the order they are offered, decided
/// once.
///
/// <para><b>Why this exists.</b> Wayfarer has more than one surface onto the same set of actions, and
/// written more than once they drift: a condition tightened in one place, a count in a label that
/// stops matching, an entry that exists on one surface and not the other. So the conditions and the
/// words live here and every menu is a renderer.</para>
///
/// <para><b>Hidden, not disabled.</b> Every action that does not apply right now is absent: no
/// teleport suggestion, nothing engaged to stop.</para>
///
/// <para><b>What is never absent.</b> <see cref="Subject"/> and the Main Scenario entry. Those two
/// are the answers to "what am I doing" and "get me out of this", and both used to be conditional:
/// the first on the followed thing having a Journal page, the second on nothing being engaged —
/// which is how a controller player ended up on a plate that did nothing and a way home that was
/// greyed out.</para>
///
/// <para>Every list is rebuilt at the moment a menu opens, never cached: a player who opens a menu,
/// walks into another zone and then confirms must not act on what was true when they opened
/// it.</para></summary>
internal sealed class GuidanceActions(
    ModuleRegistry modules,
    QuestHelperConfig cfg,
    IClientState clientState,
    Action openSettings,
    IPluginLog log)
{
    /// <summary>The one word for going back to the default loop, on every surface that offers it.
    /// </summary>
    private const string MainScenarioLabel = "Main Scenario";

    /// <summary>The guidance the actions read and drive, or null when Quest Helper is switched off —
    /// in which case there is nothing to offer at all, since every action below ultimately reads or
    /// drives it.</summary>
    public QuestNavigator? Navigator =>
        modules.Get<QuestHelperModule>() is { Enabled: true } questHelper ? questHelper.Navigator : null;

    /// <summary>What to do about where the player is going right now: take the teleport guidance is
    /// recommending, queue the duty its objective is inside, or stop what is running.</summary>
    public IReadOnlyList<GuidanceAction> Route()
    {
        var actions = new List<GuidanceAction>();
        if (Navigator is not { } navigator)
        {
            return actions;
        }

        var state = navigator.Current;
        Add(actions, Teleport(state));
        Add(actions, DutyFinder(state));

        // The universal exit, shown whenever anything is engaged, since ClearPickup() is the one
        // release valve for it.
        if (state.Engaged)
        {
            actions.Add(new GuidanceAction("Stop", navigator.ClearPickup));
        }

        return actions;
    }

    /// <summary>What Wayfarer can be told to follow. One source, so every surface that offers the
    /// choice means the same word by it.</summary>
    public IReadOnlyList<GuidanceAction> Follow()
    {
        var actions = new List<GuidanceAction>();
        if (Navigator is null)
        {
            return actions;
        }

        // Wayfarer has no "following nothing" state: not following anything in particular IS the
        // main scenario, which is why this is a choice rather than a way to clear one. Listed
        // unconditionally and performing BOTH halves of the reset — see MainScenarioReturn for why
        // the two are independent.
        actions.Add(new GuidanceAction(MainScenarioLabel, ReturnToMainScenario));
        return actions;
    }

    /// <summary>The doors onto Wayfarer's own surfaces, and the one reset that is not a stop.</summary>
    public IReadOnlyList<GuidanceAction> Windows()
    {
        var actions = new List<GuidanceAction>
        {
            new("Open Settings", openSettings),
        };

        // Nothing to reset when following the main scenario is already exactly what is happening.
        // Otherwise it is offered — including while something is engaged, which this used to exclude
        // on the reasoning that "Stop covers the engaged case". Stop does cover it, but the player
        // asking to go back to the main scenario should not have to know that the way to do it is
        // called Stop, and the condition that excluded it is the same one that greyed the switcher's
        // own entry out.
        if (Navigator is { MainScenarioReset.Acts: true })
        {
            actions.Add(new GuidanceAction(MainScenarioLabel, ReturnToMainScenario));
        }

        return actions;
    }

    /// <summary>What a press on the subject opens: the game's own Journal when a quest is being
    /// followed, and otherwise Wayfarer's own settings.
    ///
    /// <para><b>Never null, and that is the point.</b> This used to be <c>Journal()</c>, absent
    /// whenever what was being followed had no quest row. The plate called it anyway: the callback
    /// existed, so the plate grew its hit box and its controller anchor, took the press, and did
    /// nothing at all. A control that looks live and is not is worse than one that is visibly
    /// unavailable, so there is now always somewhere for the press to go.</para></summary>
    public GuidanceAction Subject()
    {
        if (Navigator?.Current.QuestId is { } questId and > 0)
        {
            return new GuidanceAction("Open Journal", () => QuestJournalAction.Execute(questId));
        }

        // The floor. With no quest row behind the subject there is nothing for the Journal to select,
        // so the press opens settings rather than opening the Journal at whatever page it last had.
        return new GuidanceAction("Open Settings", openSettings);
    }

    private static void Add(List<GuidanceAction> actions, GuidanceAction? action)
    {
        if (action is not null)
        {
            actions.Add(action);
        }
    }

    /// <summary>The guided quest's objective is inside instanced content it can be queued for right
    /// now — see <c>DutyObjectiveGuidance</c>. The window's Quests tab has the same button.</summary>
    private static GuidanceAction? DutyFinder(NavigationState state)
    {
        if (state.DutyContentFinderConditionId is not { } cfcId)
        {
            return null;
        }

        return new GuidanceAction("Open Duty Finder", () => DutyFinderAction.Execute(cfcId));
    }

    /// <summary>Teleport routes through the existing <see cref="TeleportAction"/> gate — the
    /// click-to-teleport setting, login state and attunement — so the plugin's only server-affecting
    /// action stays exactly that, whichever menu asked for it.</summary>
    private GuidanceAction? Teleport(NavigationState state)
    {
        if (!string.Equals(state.Mode, NavigationState.Modes.OtherZone, StringComparison.Ordinal)
            || !cfg.ClickTeleportEnabled
            || !state.AetheryteUnlocked
            || state.AetheryteId is not { } aetheryteId
            || state.AetheryteName is not { } aetheryteName)
        {
            return null;
        }

        return new GuidanceAction(
            $"Teleport to {aetheryteName}", () => TeleportAction.Execute(aetheryteId, cfg, clientState, log));
    }

    /// <summary>Both halves of the reset, always, in this order: release whatever is engaged, then
    /// drop the followed quest. Either one alone leaves the player somewhere they did not ask to
    /// be.</summary>
    private void ReturnToMainScenario()
    {
        if (Navigator is not { } navigator)
        {
            return;
        }

        navigator.ClearPickup();
        navigator.FollowedOverride = null;
    }
}
