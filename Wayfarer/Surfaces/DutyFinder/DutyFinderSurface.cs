using System.Numerics;
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
internal sealed class DutyFinderSurface(IFramework framework, IAddonEventManager events, IPluginLog log) : IDutyFinder, IAsyncDisposable
{
    private const string AddonName = "ContentsFinder";

    private readonly List<IDutyRowMarks> contributors = [];
    private readonly Dictionary<uint, RowStrip> stripsByRow = [];

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
            OnUpdate = Redrawn,
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

    /// <summary>Lays a row's strip out afresh: everything the game would have drawn, drawn again
    /// by us, beside the marks the modules asked for.</summary>
    private unsafe void Set(AddonContentsFinder* addon, DutyFinderRow row)
    {
        try
        {
            var wanted = Wanted(row);
            var lit = row.Places.Count(place => place.Lit);
            var strip = StripLayout.Place(wanted.Count, [], lit, DutyFinderMetrics.Strip);
            For(row).Lay(row, wanted, strip, addon, events);
            if (addon != null)
            {
                // The patches the pointer is tested against have moved, and the window keeps its
                // own list of them.
                addon->AtkUnitBase.UpdateCollisionNodeList(false);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "a Duty Finder row could not be marked, so it stays unmarked.");
        }
    }

    /// <summary>This row's strip, made the first time the row is drawn and kept while the window
    /// is open. A row is handed round as the list scrolls, so a strip belongs to the row rather
    /// than to any one duty.</summary>
    private RowStrip For(DutyFinderRow row) =>
        stripsByRow.TryGetValue(row.NodeId, out var held) ? held : stripsByRow[row.NodeId] = new RowStrip();

    /// <summary>Gives a row back exactly as it was found, for a row that no longer wants marking.
    /// The game says when, which is what makes handing a row on safe.</summary>
    private unsafe void Clear(AddonContentsFinder* addon, DutyFinderRow row)
    {
        if (!stripsByRow.Remove(row.NodeId, out var strip))
        {
            return;
        }

        strip.Dispose();
        if (addon != null)
        {
            addon->AtkUnitBase.UpdateCollisionNodeList(false);
        }
    }

    /// <summary>The window has redrawn a row of its own accord, which it does whenever one is
    /// chosen or let go of, and put back the pictures we drew in place of. They are hidden again,
    /// or the row would show both at once.</summary>
    private unsafe void Redrawn(AddonContentsFinder* addon)
    {
        foreach (var strip in stripsByRow.Values)
        {
            strip.Reassert();
        }
    }

    /// <summary>The window has closed and taken its rows with it, so the marks hung off them are
    /// done with too.</summary>
    private unsafe void Closed(AddonContentsFinder* addon) => FreeAll(putBack: false);

    /// <summary>Gives every row back and frees everything we drew on it. Safe to call twice, and
    /// safe to call with nothing to free.</summary>
    /// <param name="putBack">Whether the game's own parts are put back as well. They are, unless
    /// the window itself is closing: its rows are going with it and are not ours to write to on
    /// the way out.</param>
    private void FreeAll(bool putBack = true)
    {
        foreach (var strip in stripsByRow.Values)
        {
            if (putBack)
            {
                strip.Dispose();
            }
        }

        stripsByRow.Clear();
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
