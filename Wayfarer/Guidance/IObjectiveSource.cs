namespace Wayfarer.Guidance;

/// <summary>A module's guidance half: something that can say what to go to. The app's contract
/// that a module fulfils.</summary>
public interface IObjectiveSource
{
    /// <summary>What a surface calls this source: "Quests", "Hunting Log".</summary>
    string Name { get; }

    /// <summary>What to go to right now, or null for nothing. Read every frame while this source
    /// holds focus; the source does its own diffing of the game's state and hands back the same
    /// objective until something changed.</summary>
    Objective? Current { get; }

    /// <summary>The player pressed a step whose action is the source's own. Only called for an
    /// <see cref="EntryAction.Own"/>; every other action names something the app performs.</summary>
    void PressEntry()
    {
    }

    /// <summary>The player pressed a route line whose press is the source's own. Only called for a
    /// <see cref="Presentation.RoutePress.Own"/>.</summary>
    void PressRoute()
    {
    }

    /// <summary>The player pressed the objective's headline. What that means belongs to the source
    /// that made the objective: opening the quest's journal page, the hunting log, the map. A source
    /// that marks nothing pressable never has this called.</summary>
    void PressHeadline()
    {
    }

    /// <summary>Another source claimed focus. Reset your own state and your own UI here; you will
    /// not be read again until you claim focus back.</summary>
    void Displaced();
}
