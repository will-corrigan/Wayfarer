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
/// knowing what the other asked for, so neither does: they say what they want drawn and this
/// places all of it together.</para>
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

    /// <summary>TEMPORARY. How far along a row's parts to read when describing one. The game does
    /// not say how many there are, so this is far enough to reach past the icons.</summary>
    private const int DescribeDepth = 24;

    private readonly List<IDutyRowMarks> contributors = [];
    private readonly Dictionary<uint, List<IconImageNode>> marksByRow = [];

    private NativeListController<AddonContentsFinder, DutyFinderRow>? rows;
    private bool disposed;
    private bool described;

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

        var mark = new IconImageNode
        {
            Size = new(DutyFinderMetrics.BadgeSize, DutyFinderMetrics.BadgeSize),
            IsVisible = false,
        };
        mark.AttachNode(name, NodePosition.AfterTarget);
        return mark;
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
        FreeAll();
    }

    /// <summary>Everything wanted on a row, in the order the modules were asked. What one module
    /// wants is never placed without the rest, because they share the room it goes in.</summary>
    private List<uint> Wanted(DutyFinderRow row)
    {
        var wanted = new List<uint>();
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
        Describe(row);
        return Wanted(row).Count > 0;
    }

    /// <summary>Marks a row the game has just drawn: as many marks as were asked for, laid out
    /// together in the space the row keeps for its icons.</summary>
    private unsafe void Set(AddonContentsFinder* addon, DutyFinderRow row)
    {
        try
        {
            var wanted = Wanted(row);
            var marks = For(row, wanted.Count);
            var strip = StripLayout.Place(wanted.Count, DutyFinderMetrics.StripLeft, DutyFinderMetrics.StripRight, DutyFinderMetrics.StripPitch);
            for (var index = 0; index < marks.Count; index++)
            {
                marks[index].IconId = wanted[index];
                marks[index].Position = new(strip.At(index), DutyFinderMetrics.StripTop);
                marks[index].IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "a Duty Finder row could not be marked, so it stays unmarked.");
        }
    }

    /// <summary>This row's marks, made up to the number wanted. A row that has carried marks before
    /// keeps the nodes it had rather than making them again.</summary>
    private unsafe List<IconImageNode> For(DutyFinderRow row, int wanted)
    {
        if (!marksByRow.TryGetValue(row.NodeId, out var marks))
        {
            marks = [];
            marksByRow[row.NodeId] = marks;
        }

        while (marks.Count < wanted && Attach(row) is { } made)
        {
            marks.Add(made);
        }

        // More than are wanted now, from a row that carried more before it was handed on.
        for (var index = wanted; index < marks.Count; index++)
        {
            marks[index].IsVisible = false;
        }

        return marks.GetRange(0, Math.Min(wanted, marks.Count));
    }

    /// <summary>Takes every mark back off a row that no longer wants any.</summary>
    private unsafe void Clear(AddonContentsFinder* addon, DutyFinderRow row)
    {
        if (!marksByRow.Remove(row.NodeId, out var marks))
        {
            return;
        }

        foreach (var mark in marks)
        {
            mark.Dispose();
        }
    }

    /// <summary>TEMPORARY. Writes out the parts of a row once, so where the game keeps its own
    /// strip of row icons can be read off rather than guessed at. Delete once the answer is in
    /// <see cref="DutyFinderMetrics"/>.</summary>
    private unsafe void Describe(DutyFinderRow row)
    {
        if (described || row.NodeList == null)
        {
            return;
        }

        described = true;
        for (var index = 0; index < DescribeDepth; index++)
        {
            var node = row.NodeList[index];
            if (node != null)
            {
                log.Information($"duty row part {index}: type {node->Type} at ({node->X}, {node->Y}) {node->Width}x{node->Height} shown {node->IsVisible()}");
            }
        }
    }

    /// <summary>Frees every mark, and safe to call when there is nothing to free.</summary>
    private void FreeAll()
    {
        foreach (var mark in marksByRow.Values.SelectMany(marks => marks))
        {
            mark.Dispose();
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
