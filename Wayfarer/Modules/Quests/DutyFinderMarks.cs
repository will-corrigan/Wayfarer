using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Wayfarer.Modules.Quests;

/// <summary>Marks the Duty Finder's rows for duties an accepted quest still leads to.
///
/// <para>The window says nothing about it: a duty a quest is waiting behind looks exactly like one
/// the player queued for on a whim. The mark is the game's own for that quest, the one it draws
/// over whoever offers it, so nothing here decides what a quest looks like.</para>
///
/// <para>Rows are not ours and are reused as the list scrolls, so nothing is kept about a row but
/// the mark hung on it and the width taken from its words to make room. Both are given back the
/// moment the row stops being one of ours, and on the window closing every mark is let go while
/// the row it hung on is still alive to be let go of.</para></summary>
internal sealed unsafe class DutyFinderMarks : IDisposable
{
    /// <summary>How big the mark is when the row will not say how big its own are. Only a row
    /// being built answers nothing, and it is asked again the moment it is drawn.</summary>
    private const float MarkSizeUnknown = 20f;

    /// <summary>The window whose rows are marked.</summary>
    private const string Window = "ContentsFinder";

    /// <summary>Which of the window's nodes holds the list of duties.</summary>
    private const uint DutyListNode = 6;

    private readonly QuestDuties duties;
    private readonly Dictionary<uint, IconImageNode> marks = [];
    private readonly Dictionary<uint, float> widths = [];

    private NativeListController<AddonContentsFinder, DutyRow>? rows;
    private AddonController<AddonContentsFinder>? window;

    public DutyFinderMarks(QuestDuties duties)
    {
        this.duties = duties;

        rows = new NativeListController<AddonContentsFinder, DutyRow>
        {
            AddonName = Window,
            GetPopulatorNode = List,
            ShouldModifyElement = Wanted,
            UpdateElement = Mark,
            ResetElement = Unmark,
        };

        window = new AddonController<AddonContentsFinder>
        {
            AddonName = Window,
            OnSetup = _ => duties.Read(),
            OnRefresh = _ => duties.Read(),
            OnFinalize = _ => Forget(),
        };

        rows.Enable();
        window.Enable();

        // Marking begins whenever the player switches it on, and the window may be open already.
        // Its rows were drawn before there was anything to draw them through, so the list is asked
        // to build itself again rather than left showing rows that quietly have no marks.
        Restate();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The list controller goes first: it is what calls back into this, and a callback arriving
        // while the marks are being let go would be handing out nodes that are already gone.
        rows?.Dispose();
        rows = null;
        window?.Dispose();
        window = null;
        Forget();
    }

    /// <summary>Has the window build its list again, if it is open. Does nothing when it is not:
    /// the list is built through the hook the next time it opens.</summary>
    private static void Restate()
    {
        var agent = AgentContentsFinder.Instance();
        if (agent is not null && agent->IsAgentActive())
        {
            agent->Refresh();
        }
    }

    /// <summary>The thing in the window that draws the rows, or null when the window is not built
    /// enough to have one. Every step of the way down to it can be absent while the window is
    /// being put together, and it is asked for while that is happening.</summary>
    private static AtkComponentListItemRenderer* List(AddonContentsFinder* addon)
    {
        if (addon is null || addon->DutyList is null)
        {
            return null;
        }

        return addon->DutyList->GetComponentItemRendererById(DutyListNode);
    }

    /// <summary>Whether this row is one a quest is waiting behind. Asked of every row the list
    /// draws, so it answers no as early as it can.</summary>
    private bool Wanted(AddonContentsFinder* addon, DutyRow row) =>
        duties.Any && row.Finder is { } finder && duties.Behind(finder) is not null;

    /// <summary>Hangs the quest's own mark at the end of the row's words, taking its width from
    /// them so the two do not overlap. A row already marked is only told which mark to show: the
    /// list reuses its rows as it scrolls, and taking the width twice would eat the words.</summary>
    private void Mark(AddonContentsFinder* addon, DutyRow row)
    {
        var words = row.Words;
        if (words is null || row.Finder is not { } finder || duties.Behind(finder) is not { } quest)
        {
            return;
        }

        if (marks.TryGetValue(row.NodeId, out var already))
        {
            already.IconId = quest.Icon;
            already.IsVisible = true;
            return;
        }

        // The size the game makes its own marks on this row, taken off one of them rather than
        // chosen: a number of ours is a number that is right until the game changes a window. The
        // places it keeps for them are taller than they are wide, and the mark is square, so it is
        // the width that says how big and the rest of the height that says how far down.
        var was = words->Width;
        var slot = row.IconSlot;
        var size = slot is not null && slot->Width > 0 ? slot->Width : MarkSizeUnknown;
        var top = slot is null ? 0f : slot->Y + ((slot->Height - size) / 2f);

        // Hung just before the first of the game's own marks rather than at the end of the words,
        // because the words are not the same width on every row -- a row the game has marks of its
        // own on gives up room for them -- and marks that each sat at the end of their own row
        // would step up and down the list instead of standing in a line.
        var left = slot is null ? words->X + words->Width - size : slot->X - size;

        // The words give up only what they have to: enough that a long name is shortened by the
        // game before it reaches the mark, and nothing at all when it already stops short.
        var room = (ushort)MathF.Max(0f, left - words->X);
        if (words->Width > room)
        {
            words->Width = room;
        }

        var mark = new IconImageNode
        {
            Size = new Vector2(size, size),
            Position = new Vector2(left, top),
            IconId = quest.Icon,
            FitTexture = true,
            IsVisible = true,
        };

        mark.AttachNode(row.Words, NodePosition.AfterTarget);
        widths[row.NodeId] = was;
        marks[row.NodeId] = mark;
    }

    /// <summary>Gives a row back what was taken from it: its words their width, and the mark to be
    /// let go of.</summary>
    private void Unmark(AddonContentsFinder* addon, DutyRow row)
    {
        if (!marks.Remove(row.NodeId, out var mark))
        {
            return;
        }

        // Exactly how wide the words were, not how wide they would be made now: the row may have
        // been measured differently when the mark was hung on it.
        var words = row.Words;
        if (widths.Remove(row.NodeId, out var was) && words is not null)
        {
            words->Width = (ushort)was;
        }

        mark.Dispose();
    }

    /// <summary>Lets go of every mark. The rows they hang on are the game's and are going or gone,
    /// so their widths are not given back here — the window rebuilds its rows either way.</summary>
    private void Forget()
    {
        foreach (var mark in marks.Values)
        {
            mark.Dispose();
        }

        marks.Clear();
        widths.Clear();
    }
}
