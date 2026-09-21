using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;

namespace Wayfarer.Modules.Quests;

/// <summary>One row of the Duty Finder's list, as much of it as marking a row needs: the words
/// naming the duty, and which Duty Finder entry the row stands for.
///
/// <para>A row does not carry its own entry. It carries a number into the agent's list of what it
/// is showing, counted from one, and the entry is read from there. Both may be absent while the
/// game is building the list, which is why every reader here can answer with nothing.</para>
/// </summary>
internal sealed unsafe class DutyRow : ListItemData
{
    /// <summary>Which of the row's nodes holds the duty's name.</summary>
    private const int DutyNameNode = 3;

    /// <summary>Which of the row's numbers counts into the agent's list, from one.</summary>
    private const int ContentIndexValue = 1;

    /// <summary>The first of the places the row keeps for the marks the game puts on it. Asked
    /// only how big it is and where it sits: a mark of ours beside the game's own should be the
    /// size the game makes them, and the row is the only thing that knows what that is.</summary>
    private const int FirstIconSlot = 5;

    /// <summary>The words naming the duty, or null while the row is being built.</summary>
    public AtkTextNode* Words => GetNode<AtkTextNode>(DutyNameNode);

    /// <summary>One of the places the row keeps for a mark, to be measured against. Null while the
    /// row is being built.</summary>
    public AtkResNode* IconSlot => GetNode<AtkResNode>(FirstIconSlot);

    /// <summary>The Duty Finder entry this row stands for, or null when the row is not showing
    /// one — the list holds headings and blanks as well as duties.</summary>
    public uint? Finder
    {
        get
        {
            if (ItemInfo == null)
            {
                return null;
            }

            var counted = GetNumber(ContentIndexValue);
            var agent = AgentContentsFinder.Instance();
            if (counted == 0 || agent == null || counted > agent->ContentList.Count)
            {
                return null;
            }

            var content = agent->ContentList[(int)counted - 1].Value;
            return content == null ? null : content->Id.Id;
        }
    }
}
