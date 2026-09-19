using Wayfarer.App.Guidance;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Presentation;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>What a press on the block does. Each press reads the guidance at the moment it happens
/// rather than anything captured when the line was drawn, so a press can never act on a route or a
/// step that has since moved on. A press with nothing behind it does nothing at all.</summary>
internal sealed class GuidancePresses(IGuidance guidance, IActions actions)
{
    /// <summary>What the game's own say command looks like in the chat box.</summary>
    private const string SayCommand = "/say ";

    /// <summary>The step's own press: use the key item, perform the emote, or write the phrase into
    /// the chat box for the player to send.</summary>
    public void Entry()
    {
        switch (guidance.Current?.Target?.Action)
        {
            case EntryAction.UseItem item:
                actions.UseItem(item.ItemId);
                break;
            case EntryAction.Emote emote:
                actions.Emote(emote.EmoteId);
                break;
            case EntryAction.Say say:
                actions.FillChat(SayCommand + say.Phrase);
                break;
            default:
                break;
        }
    }

    /// <summary>The route's press: cast the teleport, or open the Duty Finder at the duty.</summary>
    public void Route()
    {
        switch (RouteWords.Compose(guidance.Current)?.Press)
        {
            case RoutePress.Teleport teleport:
                actions.TeleportTo(teleport.AetheryteId);
                break;
            case RoutePress.OpenDuty duty:
                actions.OpenDutyFinder(duty.DutyId);
                break;
            default:
                break;
        }
    }

    /// <summary>The headline's press: open the page the guidance is about.</summary>
    public void Headline()
    {
        switch (guidance.Current?.Objective.Action)
        {
            case HeadlineAction.OpenQuestJournal journal:
                actions.OpenQuestJournal(journal.QuestId);
                break;
            default:
                break;
        }
    }
}
