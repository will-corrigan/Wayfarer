using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>One row of the Duty Finder, and what it is about.
///
/// <para>A row does not hold the duty it shows; it holds its place in the list the Duty Finder's
/// agent built, counted from one. That list is where the duty is, so the row is asked for its
/// place and the agent for what stands there. This is the game's own answer, and nothing has to be
/// matched or guessed.</para></summary>
internal sealed unsafe class DutyFinderRow : ListItemData
{
    /// <summary>The row's own name, the text the player reads. The mark is hung beside it.</summary>
    public AtkTextNode* NameNode => GetNode<AtkTextNode>(DutyFinderMetrics.RowNameNodeIndex);

    /// <summary>The duty this row queues for, or null when the row is not for one: a roulette, or
    /// a place in the list the agent no longer has anything at.</summary>
    public uint? Duty
    {
        get
        {
            var agent = AgentContentsFinder.Instance();
            if (agent == null)
            {
                return null;
            }

            // The row counts its place from one, and the list from zero.
            var place = (long)GetNumber(DutyFinderMetrics.RowContentIndexValue) - 1;
            if (place < 0 || place >= agent->ContentList.LongCount)
            {
                return null;
            }

            var content = agent->ContentList[place].Value;
            return content != null && content->Id.ContentType == ContentsType.Regular && content->Id.Id != 0
                ? content->Id.Id
                : null;
        }
    }
}
