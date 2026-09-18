using Wayfarer.Core.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's guidance half. Will read the game's quest state — which quest is
/// the main scenario, its step, the step's text and places — and hand the app an objective when
/// something changes. Empty for now: it claims nothing and offers nothing, so the shell runs with
/// the module loaded and guides to nothing, exactly as the banner does while it shows "???".</summary>
internal sealed class QuestObjectives : IObjectiveSource
{
    /// <inheritdoc/>
    public string Name => "Quests";

    /// <inheritdoc/>
    public Objective? Current => null;

    /// <inheritdoc/>
    public void Displaced()
    {
    }
}
