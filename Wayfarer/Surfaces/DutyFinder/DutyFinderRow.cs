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

    /// <summary>Where this row's icon slots are standing empty, left to right: the room a mark can
    /// go in without anything of the game's having to move.</summary>
    public List<float> DarkSlots => [.. Slots.Select(Unused).OfType<float>()];

    /// <summary>The game's own icons this row is showing, left to right. They belong to the game:
    /// they are moved and resized only when there is no other room, and always put back.</summary>
    public List<nint> LitSlots => [.. Slots.Where(Lit).Select(slot => (nint)slot.Value)];

    /// <summary>Every part the row is built from, as the row itself counts them. The array the game
    /// hands over when it draws a row does not say how long it is, so the component it is drawn by
    /// is asked instead, which knows.</summary>
    public List<nint> Parts
    {
        get
        {
            var component = Component;
            if (component == null)
            {
                return [];
            }

            var parts = new List<nint>(component->UldManager.NodeListCount);
            for (var index = 0; index < component->UldManager.NodeListCount; index++)
            {
                if (component->UldManager.NodeList[index] != null)
                {
                    parts.Add((nint)component->UldManager.NodeList[index]);
                }
            }

            return parts;
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

    /// <summary>The row as the component it is, which is what names a part by its id. Null when
    /// this row is not drawn by one, which is the game's way of saying there is nothing here.
    /// A pointer cannot be asked with <c>?.</c>, so it is asked the long way.</summary>
    private AtkComponentBase* Component =>
        ItemRenderer == null ? null : &ItemRenderer->AtkComponentButton.AtkComponentBase;

    /// <summary>Whether the game is using a slot.</summary>
    private static bool Lit(Pointer<AtkResNode> slot) => slot.Value != null && slot.Value->IsVisible();

    /// <summary>Where a slot is standing empty, or null when the game is using it. A slot the row
    /// does not have at all is room the layout file says is there, so it is offered.</summary>
    /// <param name="slot">The slot itself, which the row may not have.</param>
    /// <param name="place">Which of the strip's slots it is, counted from the left.</param>
    private static float? Unused(Pointer<AtkResNode> slot, int place) => slot.Value switch
    {
        null => DutyFinderMetrics.StripLeft + (place * DutyFinderMetrics.StripPitch),
        var node when node->IsVisible() => null,
        var node => node->X,
    };
}
