using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    /// <summary>How big the mark is when the row will not say how tall it is. Only a row being
    /// built answers nothing, and it is asked again the moment it is drawn.</summary>
    private const float MarkSizeUnknown = 20f;

    /// <summary>The window whose rows are marked.</summary>
    private const string Window = "ContentsFinder";

    /// <summary>Which of the window's nodes holds the list of duties.</summary>
    private const uint DutyListNode = 6;

    private readonly QuestDuties duties;
    private readonly QuestJournal journal;
    private readonly Dictionary<uint, IconButtonNode> marks = [];
    private readonly Dictionary<uint, float> widths = [];

    private NativeListController<AddonContentsFinder, DutyRow>? rows;
    private AddonController<AddonContentsFinder>? window;

    public DutyFinderMarks(QuestDuties duties, QuestJournal journal)
    {
        this.duties = duties;
        this.journal = journal;

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

    private static AtkComponentListItemRenderer* List(AddonContentsFinder* addon) =>
        addon is null ? null : addon->DutyList->GetComponentItemRendererById(DutyListNode);

    /// <summary>Whether this row is one a quest is waiting behind. Asked of every row the list
    /// draws, so it answers no as early as it can.</summary>
    private bool Wanted(AddonContentsFinder* addon, DutyRow row) =>
        duties.Any && row.Finder is { } finder && duties.Behind(finder) is not null;

    /// <summary>Hangs the quest's own mark at the end of the row's words, taking its width from
    /// them so the two do not overlap. A row already marked is only told which mark to show: the
    /// list reuses its rows as it scrolls, and taking the width twice would eat the words.</summary>
    private void Mark(AddonContentsFinder* addon, DutyRow row)
    {
        if (row.Finder is not { } finder || duties.Behind(finder) is not { } quest || row.Words is null)
        {
            return;
        }

        if (marks.TryGetValue(row.NodeId, out var already))
        {
            already.IconId = quest.Icon;
            already.OnClick = () => journal.Open(quest.QuestId);
            already.IsVisible = true;
            return;
        }

        // As tall as the row's own words, so the mark is the height of the line whatever the line
        // is: a number of our own is a number that is right until the game changes a window.
        var words = row.Words;
        var size = words->Height > 0 ? words->Height : MarkSizeUnknown;

        // Taken off the end of the words rather than laid over them, so a long name is shortened
        // by the game the way it already shortens one that will not fit.
        words->Width = (ushort)MathF.Max(0f, words->Width - size);

        var mark = new IconButtonNode
        {
            Size = new Vector2(size, size),
            Position = new Vector2(words->X + words->Width, 0f),
            IconId = quest.Icon,
            OnClick = () => journal.Open(quest.QuestId),
            IsVisible = true,
        };

        // A button of this kind is drawn as a frame with a picture inset in it, and it keeps eight
        // yalms of itself on every side for the frame. Here the mark is the button — it sits among
        // the game's own rows and a frame round it would look like nothing else there — so the
        // frame is left off and the picture is given the whole of it back.
        mark.BackgroundNode.IsVisible = false;
        mark.ImageNode.Size = new Vector2(size, size);
        mark.ImageNode.Position = Vector2.Zero;
        mark.ImageNode.FitTexture = true;

        mark.AttachNode(row.Words, NodePosition.AfterTarget);
        widths[row.NodeId] = size;
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

        // Exactly what was taken, not what would be taken now: the row may have been measured
        // differently when the mark was hung on it.
        var words = row.Words;
        if (widths.Remove(row.NodeId, out var taken) && words is not null)
        {
            words->Width = (ushort)(words->Width + taken);
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
