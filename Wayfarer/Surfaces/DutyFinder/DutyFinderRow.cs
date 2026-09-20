using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
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
    /// <summary>The row's own name, the text the player reads. Marks are hung beside it, and when
    /// they will not otherwise fit it is what gives up the room.</summary>
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
            var content = place >= 0 && place < agent->ContentList.LongCount
                ? agent->ContentList[place].Value
                : null;

            return content != null && content->Id is { ContentType: ContentsType.Regular, Id: not 0 } duty
                ? duty.Id
                : null;
        }
    }

    /// <summary>The component a row is drawn by, for anything that needs to walk its parts.</summary>
    public AtkComponentBase* Owner => Component;

    /// <summary>The row's icon places, left to right, as the game left them: what it would draw in
    /// each and the patch of row the pointer is tested against there.</summary>
    public List<RowSlot> Places
    {
        get
        {
            var places = new List<RowSlot>(DutyFinderMetrics.GameIconNodeIds.Length);
            foreach (var slot in Slots)
            {
                if (slot.Value is null)
                {
                    continue;
                }

                nint picture = 0, touch = 0;
                for (var held = slot.Value->ChildNode; held != null; held = held->PrevSiblingNode)
                {
                    if (held->GetAsAtkCollisionNode() != null)
                    {
                        touch = (nint)held;
                    }
                    else if (held->GetAsAtkImageNode() != null && (picture == 0 || held->IsVisible()))
                    {
                        picture = (nint)held;
                    }
                }

                places.Add(new RowSlot((nint)slot.Value, picture, touch));
            }

            return places;
        }
    }

    /// <summary>The row's icon slots in the order they sit, whether or not the row has them. Read
    /// fresh, because a row is the game's and may be anything by the next drawing.</summary>
    private List<Pointer<AtkResNode>> Slots
    {
        get
        {
            var component = Component;
            var slots = new List<Pointer<AtkResNode>>(DutyFinderMetrics.GameIconNodeIds.Length);
            foreach (var id in DutyFinderMetrics.GameIconNodeIds)
            {
                slots.Add(component == null ? null : component->GetNodeById(id));
            }

            return slots;
        }
    }

    /// <summary>The component a row is drawn by, which is what names a part by its id.
    ///
    /// <para>A list hands over one of two things and never both: the renderer it draws a row with,
    /// or the row's own record. This list hands over the record, so there is no renderer to ask and
    /// the component is found by climbing from a part of the row we were given until a part of it
    /// turns out to be one. Asking the renderer alone answered nothing at all here, and a row whose
    /// parts cannot be named looks exactly like a row with none.</para></summary>
    private AtkComponentBase* Component
    {
        get
        {
            if (ItemRenderer != null)
            {
                return &ItemRenderer->AtkComponentButton.AtkComponentBase;
            }

            for (var node = (AtkResNode*)NameNode; node != null; node = node->ParentNode)
            {
                var owner = node->GetAsAtkComponentNode();
                if (owner != null && owner->Component != null)
                {
                    return owner->Component;
                }
            }

            return null;
        }
    }

    /// <summary>Whether the game is using a slot.
    ///
    /// <para>The slot itself is always shown, on every row, whether or not the game has put
    /// anything in it. What it holds is what is switched on and off, so a slot is in use when
    /// something inside it is being drawn. Asking the slot answered yes for every row and is why
    /// a mark once landed on top of an icon.</para></summary>
    private static bool Lit(Pointer<AtkResNode> slot) => slot.Value != null && Draws(slot.Value);

    /// <summary>Whether anything inside a slot is being drawn.</summary>
    private static bool Draws(AtkResNode* slot)
    {
        for (var held = slot->ChildNode; held != null; held = held->PrevSiblingNode)
        {
            if (held->GetAsAtkImageNode() != null && held->IsVisible())
            {
                return true;
            }
        }

        return false;
    }
}
