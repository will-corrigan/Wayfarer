using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Wayfarer.Modules.Quests;

/// <summary>Remembers what the player has interacted with, by watching the game rather than the
/// player: the moment the game says they are occupied in a quest event, whatever they had targeted
/// is what they acted on.
///
/// <para>This is the only signal there is. A search area's decoys are identical in the data and
/// identical in the world, the quest's own progress records nothing when one of them answers with
/// nothing, and a tried one stays there looking exactly as it did. Nothing but watching the player
/// distinguishes the one they have already walked to from the one they have not.</para>
///
/// <para>Anything the player acts on is remembered, whether or not it answered: what was tried is
/// the question, and whether it worked answers itself when the step moves on and the slate is
/// wiped. Only the things a step is actually looking for are ever asked about, so remembering more
/// than those costs nothing.</para>
///
/// <para>Dalamud raises the condition itself, so no game function is hooked and there is nothing
/// to leave behind but an unsubscribe.</para></summary>
internal sealed class Interactions : IInteractions, IDisposable
{
    /// <summary>The game saying the player is in an event, which is what interacting with something
    /// the world put there starts. Both are watched: a thing that answers a quest raises the quest
    /// one, and a thing that answers with nothing may raise only the plain one.</summary>
    private static readonly ConditionFlag[] InAnEvent =
    [
        ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.OccupiedInEvent,
    ];

    private readonly ICondition condition;
    private readonly ITargetManager targets;
    private readonly HashSet<uint> tried = [];

    public Interactions(ICondition condition, ITargetManager targets)
    {
        this.condition = condition;
        this.targets = targets;
        condition.ConditionChange += OnConditionChange;
    }

    /// <inheritdoc/>
    public int Count => tried.Count;

    /// <inheritdoc/>
    public bool Tried(uint baseId) => tried.Contains(baseId);

    /// <inheritdoc/>
    public void Forget() => tried.Clear();

    /// <inheritdoc/>
    public void Dispose() => condition.ConditionChange -= OnConditionChange;

    /// <summary>The event beginning is the moment to look: the target is still whatever was acted
    /// on, and by the time it ends the player may have moved on to something else.</summary>
    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (value && Array.IndexOf(InAnEvent, flag) >= 0 && targets.Target is { } acted)
        {
            tried.Add(acted.BaseId);
        }
    }
}
