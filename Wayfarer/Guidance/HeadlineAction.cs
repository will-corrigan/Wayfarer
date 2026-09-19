namespace Wayfarer.Guidance;

/// <summary>Where the objective's headline leads when pressed: the page in the game that is
/// about the thing being guided to. The module that knows the thing fills it in; the surface
/// offers the headline as a press; the app opens the page.</summary>
public abstract record HeadlineAction
{
    private HeadlineAction()
    {
    }

    /// <summary>Open the quest journal at a quest.</summary>
    /// <param name="QuestId">The quest's id, as the game's quest manager numbers it.</param>
    public sealed record OpenQuestJournal(ushort QuestId) : HeadlineAction;
}
