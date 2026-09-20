using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>One row of the Duty Finder: what it is about, and the parts of it worth reaching.
///
/// <para>A row does not hold the duty it shows; it holds its place in the list the Duty Finder's
/// agent built, counted from one. That list is where the duty is, so the row is asked for its
/// place and the agent for what stands there. This is the game's own answer, and nothing has to be
/// matched or guessed.</para>
///
/// <para>Everything here is read fresh and may be nothing: a row is the game's, it is handed round
/// as the list scrolls, and none of it may be held from one drawing to the next.</para></summary>
internal sealed unsafe class DutyFinderRow : ListItemData
{
    /// <summary>The row's own name, the text the player reads. A mark is hung beside it.</summary>
    public AtkTextNode* NameNode => GetNode<AtkTextNode>(DutyFinderMetrics.RowNameNodeIndex);

    /// <summary>The duty this row queues for, or null when the row is not for one: a roulette, or
    /// a place in the list the agent no longer has anything at.</summary>
    public uint? Duty
    {
        get
        {
            var agent = AgentContentsFinder.Instance();
            if (agent == null || ItemInfo == null)
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

    /// <summary>Where this row's icon slots are standing empty, left to right: the room a mark can
    /// go in without anything of the game's having to move.
    ///
    /// <para>The slots are reached by node id rather than by their place in the parts the row is
    /// drawn from, because the game names them in its layout and does not say where they sit in
    /// that list. Where a slot really is is read off the slot itself, so the game moving them is
    /// followed rather than argued with; only a slot the row does not have at all falls back to
    /// where the layout file puts it.</para>
    ///
    /// <para>Nothing of the game's is written to here or anywhere else. A lit slot is simply not
    /// offered, and a mark goes somewhere else.</para></summary>
    public List<float> DarkSlots
    {
        get
        {
            var dark = new List<float>();
            var component = Component;
            for (var slot = 0; slot < DutyFinderMetrics.GameIconNodeIds.Length; slot++)
            {
                var node = component == null ? null : component->GetNodeById(DutyFinderMetrics.GameIconNodeIds[slot]);
                if (node == null)
                {
                    dark.Add(DutyFinderMetrics.StripLeft + (slot * DutyFinderMetrics.StripPitch));
                }
                else if (!node->IsVisible())
                {
                    dark.Add(node->X);
                }
            }

            return dark;
        }
    }

    /// <summary>Every part the row is built from, as the row itself counts them.
    ///
    /// <para>The array the game hands over when it draws a row does not say how long it is, so it
    /// cannot be walked. The component the row is knows how many parts it has, and that is what is
    /// walked instead.</para></summary>
    public List<nint> Parts
    {
        get
        {
            var parts = new List<nint>();
            var component = Component;
            if (component == null)
            {
                return parts;
            }

            ref var uld = ref component->UldManager;
            for (var index = 0; index < uld.NodeListCount; index++)
            {
                if (uld.NodeList[index] != null)
                {
                    parts.Add((nint)uld.NodeList[index]);
                }
            }

            return parts;
        }
    }

    /// <summary>The row as the component it is, which is what names a part by its id. Null when
    /// this row is not drawn by one, which is the game's way of saying there is nothing here.</summary>
    private AtkComponentBase* Component =>
        ItemRenderer == null ? null : &ItemRenderer->AtkComponentButton.AtkComponentBase;
}
