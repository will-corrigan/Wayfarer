using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>The Duty Finder's rows, and who is allowed to draw on them.
///
/// <para>More than one module can want a duty marked — a quest left in it, a hunt target inside
/// it — and a row keeps one small strip for icons. Neither module can place its own mark without
/// knowing what the other asked for, nor without knowing which slots the game is using itself, so
/// neither does: they say what they want drawn and this places all of it together, in the slots
/// the game left dark. Nothing of the game's is moved or written to.</para>
///
/// <para>The game is what says when a row is drawn and what it is drawn as, so that is what is
/// listened to rather than the window being read every frame. A row is handed round as the list
/// scrolls, and the same telling that hands it to another duty is the one that sets its marks; a
/// row that stops wanting any is told to give them back.</para>
///
/// <para>The window is only watched while something is marking it, and every node made here is
/// freed when the row gives it back, when the window closes, when the last module lets go, or when
/// the plugin unloads.</para></summary>
internal sealed class DutyFinderSurface(IFramework framework, IPluginLog log) : IDutyFinder, IAsyncDisposable
{
    private const string AddonName = "ContentsFinder";

    private readonly List<IDutyRowMarks> contributors = [];
    private readonly Dictionary<uint, RowMarks> marksByRow = [];

    private NativeListController<AddonContentsFinder, DutyFinderRow>? rows;
    private AddonController<AddonContentsFinder>? window;
    private bool disposed;

    /// <inheritdoc/>
    public IDisposable Mark(IDutyRowMarks marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ObjectDisposedException.ThrowIf(disposed, this);

        _ = framework.RunOnFrameworkThread(() => Begin(marks));
        return new Marking(this, marks);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (rows is { } watching)
        {
            rows = null;
            await watching.DisposeAsync().ConfigureAwait(false);
        }

        if (window is { } watched)
        {
            window = null;
            await watched.DisposeAsync().ConfigureAwait(false);
        }

        await framework.RunOnFrameworkThread(() =>
        {
            contributors.Clear();
            FreeAll();
        }).ConfigureAwait(false);
    }

    /// <summary>The row the duty list fills every one of its rows in from. Everything the game
    /// draws in this list goes through it, which is what there is to listen to.</summary>
    private static unsafe AtkComponentListItemRenderer* Template(AddonContentsFinder* addon) =>
        addon == null || addon->DutyList == null
            ? null
            : addon->DutyList->GetComponentItemRendererById(DutyFinderMetrics.RowTemplateNodeId);

    /// <summary>Hangs a new mark off a row, beside its name. Its place is set when the row is laid
    /// out, so it is made where it will not be seen and moved into place after.</summary>
    private static unsafe IconImageNode? Attach(DutyFinderRow row)
    {
        var name = row.NameNode;
        if (name == null)
        {
            return null;
        }

        var mark = new IconImageNode { FitTexture = true, IsVisible = false };
        mark.AttachNode(name, NodePosition.AfterTarget);
        return mark;
    }

    /// <summary>Moves the game's own icons over, when the strip has been laid out again and they
    /// are part of it. Where each was is written down before it is asked to move.</summary>
    private static unsafe void MoveGameIcons(List<nint> lit, StripLayout.Strip strip, RowMarks held)
    {
        for (var index = 0; index < strip.GameIcons.Count && index < lit.Count; index++)
        {
            var node = (AtkResNode*)lit[index];
            if (node == null)
            {
                continue;
            }

            held.Moved.Add(RowMarks.WasAt.Of(node));
            node->SetXFloat(strip.GameIcons[index]);
            node->SetWidth((ushort)strip.Size);
            node->SetHeight((ushort)strip.Size);
        }
    }

    /// <summary>Takes from the row's name what the strip could not find anywhere else, and only
    /// when the strip says it has to. How wide the name was is written down before it is asked.</summary>
    private static unsafe void MakeRoom(DutyFinderRow row, StripLayout.Strip strip, RowMarks held)
    {
        var name = row.NameNode;
        if (strip.Left is not { } left || name == null)
        {
            return;
        }

        var wanted = left - name->AtkResNode.X;
        if (wanted > 0 && wanted < name->AtkResNode.Width)
        {
            held.Name = RowMarks.WasAt.Of(&name->AtkResNode);
            name->AtkResNode.SetWidth((ushort)wanted);
        }
    }

    /// <summary>Takes up marking on behalf of a module, and starts listening to the window if
    /// nothing else was. On the framework thread.</summary>
    private unsafe void Begin(IDutyRowMarks marks)
    {
        if (disposed)
        {
            return;
        }

        contributors.Add(marks);
        if (contributors.Count > 1)
        {
            return;
        }

        rows ??= new NativeListController<AddonContentsFinder, DutyFinderRow>
        {
            AddonName = AddonName,
            GetPopulatorNode = Template,
            ShouldModifyElement = Wants,
            UpdateElement = Set,
            ResetElement = Clear,
        };
        rows.Enable();

        // The rows a mark hangs off are the window's, and go when it closes. Nothing else tells us
        // that has happened, and a node held past it is a node pointing at freed memory.
        window ??= new AddonController<AddonContentsFinder>
        {
            AddonName = AddonName,
            OnFinalize = Closed,
        };
        window.Enable();
    }

    /// <summary>Gives up marking on behalf of a module. When it was the last, the marks come off
    /// and the window is let alone: it may still be open, so the nodes go now rather than at a
    /// close that may never come this session. On the framework thread.</summary>
    private void End(IDutyRowMarks marks)
    {
        if (disposed || !contributors.Remove(marks) || contributors.Count > 0)
        {
            return;
        }

        rows?.Disable();
        window?.Disable();
        FreeAll();
    }

    /// <summary>Everything wanted on a row, in the order the modules were asked. What one module
    /// wants is never placed without the rest, because they share the room it goes in.</summary>
    private List<DutyMark> Wanted(DutyFinderRow row)
    {
        var wanted = new List<DutyMark>();
        if (row.Duty is not { } duty)
        {
            return wanted;
        }

        foreach (var contributor in contributors)
        {
            wanted.AddRange(contributor.MarksFor(duty));
        }

        return wanted;
    }

    /// <summary>Whether this row wants marking at all. Answering no here is what has the game tell
    /// us to take old marks back off.</summary>
    private unsafe bool Wants(AddonContentsFinder* addon, DutyFinderRow row)
    {
        return Wanted(row).Count > 0;
    }

    /// <summary>Marks a row the game has just drawn: as many marks as were asked for, laid out
    /// together in the space the row keeps for its icons.</summary>
    private unsafe void Set(AddonContentsFinder* addon, DutyFinderRow row)
    {
        try
        {
            var wanted = Wanted(row);
            var lit = row.LitSlots;
            var strip = StripLayout.Place(wanted.Count, row.DarkSlots, lit.Count, DutyFinderMetrics.Strip);
            var held = For(row, wanted.Count);

            // Whatever was moved or resized last time goes back first, so the row is laid out from
            // how the game left it rather than from how we last left it.
            held.Restore();

            var marks = held.Marks;
            held.Forget();
            for (var index = 0; index < marks.Count; index++)
            {
                marks[index].IconId = wanted[index].IconId;
                marks[index].Size = new(strip.Size, strip.Size * (DutyFinderMetrics.StripSlotHeight / DutyFinderMetrics.StripPitch));
                marks[index].Position = new(strip.Marks[index], DutyFinderMetrics.StripTop);
                marks[index].IsVisible = true;
                held.Explain(marks[index], addon, wanted[index].Tooltip);
            }

            MoveGameIcons(lit, strip, held);
            MakeRoom(row, strip, held);
            if (addon != null)
            {
                // The slots carry the game's own tooltips, which are hit-tested in the order the
                // window keeps them, so it is told they have moved.
                addon->AtkUnitBase.UpdateCollisionNodeList(false);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "a Duty Finder row could not be marked, so it stays unmarked.");
        }
    }

    /// <summary>This row's marks, made up to the number wanted. A row that has carried marks before
    /// keeps the nodes it had rather than making them again.</summary>
    private unsafe RowMarks For(DutyFinderRow row, int wanted)
    {
        var held = marksByRow.TryGetValue(row.NodeId, out var carried)
            ? carried
            : marksByRow[row.NodeId] = new RowMarks([]);

        while (held.Marks.Count < wanted && Attach(row) is { } made)
        {
            held.Marks.Add(made);
        }

        // More than are wanted now, from a row that carried more before it was handed on.
        foreach (var spare in held.Marks.Skip(wanted))
        {
            spare.IsVisible = false;
        }

        return held;
    }

    /// <summary>Takes every mark back off a row that no longer wants any.</summary>
    private unsafe void Clear(AddonContentsFinder* addon, DutyFinderRow row)
    {
        if (!marksByRow.Remove(row.NodeId, out var held))
        {
            return;
        }

        held.Restore();
        held.Free();
        if (addon != null)
        {
            addon->AtkUnitBase.UpdateCollisionNodeList(false);
        }
    }

    /// <summary>The window has closed and taken its rows with it, so the marks hung off them are
    /// done with too.</summary>
    private unsafe void Closed(AddonContentsFinder* addon) => FreeAll(putBack: false);

    /// <summary>Frees every mark, and safe to call when there is nothing to free.</summary>
    /// <param name="putBack">Whether the game's own parts are to be put back as well. They are,
    /// unless the window itself is closing, in which case they are going anyway and the rows they
    /// belong to are not ours to touch on the way out.</param>
    private void FreeAll(bool putBack = true)
    {
        foreach (var held in marksByRow.Values)
        {
            if (putBack)
            {
                held.Restore();
            }

            held.Free();
        }

        marksByRow.Clear();
    }

    /// <summary>Gives up marking, from wherever the holder happens to let go.</summary>
    private void Release(IDutyRowMarks marks) => _ = framework.RunOnFrameworkThread(() => End(marks));

    /// <summary>One module's marks, taken away when it is disposed.</summary>
    private sealed class Marking(DutyFinderSurface surface, IDutyRowMarks marks) : IDisposable
    {
        private int done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref done, 1) == 0)
            {
                surface.Release(marks);
            }
        }
    }
}
