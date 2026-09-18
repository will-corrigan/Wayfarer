namespace Wayfarer.Core.Guidance;

/// <summary>A module's guidance half: something that can say what to go to. The app's contract
/// that a module fulfils, kept here in Core because the records it speaks in are here and
/// nothing about it needs the game.</summary>
public interface IObjectiveSource
{
    /// <summary>What a surface calls this source: "Quests", "Hunting Log".</summary>
    string Name { get; }

    /// <summary>What to go to right now, or null for nothing. Read every frame while this source
    /// holds focus; the source does its own diffing of the game's state and hands back the same
    /// objective until something changed.</summary>
    Objective? Current { get; }

    /// <summary>Another source claimed focus. Reset your own state and your own UI here; you will
    /// not be read again until you claim focus back.</summary>
    void Displaced();
}
