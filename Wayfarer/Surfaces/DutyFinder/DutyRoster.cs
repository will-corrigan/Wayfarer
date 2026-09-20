using System.Runtime.InteropServices;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Which duty each row of the Duty Finder is showing, by the name written on it.
///
/// <para>The window draws its rows from a list the Duty Finder's agent builds, and every entry in
/// that list carries both the name the row shows and the sheet row it stands for. Reading the name
/// off a row and asking this for it is therefore the game's own answer to what a row is about, and
/// needs nothing assumed about the order of either list.</para>
///
/// <para>What a name that more than one duty answers to means is <see cref="DutyNames"/>'s
/// rule; this only reads the list and hands it over.</para></summary>
internal sealed unsafe class DutyRoster
{
    private readonly DutyNames names = new();

    private ListShape shape;
    private bool read;

    /// <summary>Reads the agent's list again, if it is not the same list as last time. The window
    /// asks this every frame it is open and the list holds hundreds of duties, so what the list
    /// looks like is compared before any of it is read out.</summary>
    public void Reread()
    {
        var agent = AgentContentsFinder.Instance();
        var now = ShapeOf(agent);
        if (read && now == shape)
        {
            return;
        }

        shape = now;
        read = true;
        names.Clear();
        if (agent == null)
        {
            return;
        }

        foreach (var entry in agent->ContentList)
        {
            var content = entry.Value;
            if (content == null || content->Id.ContentType != ContentsType.Regular || content->Id.Id == 0)
            {
                continue;
            }

            names.Add(content->Name.ExtractText(), content->Id.Id);
        }
    }

    /// <summary>The duty a row showing these words is for, or null when the list does not say so
    /// plainly: a heading, a roulette, or a name more than one duty answers to.</summary>
    public uint? DutyNamed(string name) => names.DutyNamed(name);

    /// <summary>What the agent's list looks like without reading any of it. The game builds the
    /// list afresh whenever its filters change, which moves it in memory, so a list of the same
    /// length standing in the same place is the same list.</summary>
    private static ListShape ShapeOf(AgentContentsFinder* agent)
    {
        if (agent == null || agent->ContentList.LongCount == 0)
        {
            return default;
        }

        ref var list = ref agent->ContentList;
        return new ListShape((nint)list.First, (nint)list.Last, list.LongCount);
    }

    /// <summary>Enough of the agent's list to tell it from another one, and nothing read out of
    /// it: where it begins, where it ends, and how long it is.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ListShape(nint First, nint Last, long Count);
}
