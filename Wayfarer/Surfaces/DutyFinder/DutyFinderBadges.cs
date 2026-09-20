using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Puts a small icon on Duty Finder rows, on behalf of anything that asks. This knows
/// nothing about what the marks mean: it is told which rows the game is drawing, asks whoever is
/// marking what icon each duty should carry, and draws that.
///
/// <para>The game is what says when a row is drawn and what it is drawn as, so that is what is
/// listened to rather than the window being read every frame. A row is handed round as the list
/// scrolls, and the same telling that hands it to another duty is the one that sets its mark; a
/// row that stops wanting one is told to give it back.</para>
///
/// <para>The window is only watched while something is marking it, and every node made here is
/// freed when the row gives it back, when the window closes, when the last mark is taken away, or
/// when the plugin unloads.</para>
///
/// <para>The mark sits on the corner of the game's own icon at the left of a row. Other plugins
/// mark these rows too, and they put their marks after the name and take width off it to make
/// room; nothing here touches the name or its width, so both can mark the same row.</para></summary>
internal sealed class DutyFinderBadges(IFramework framework, IPluginLog log) : IDutyBadges, IAsyncDisposable
{
    private const string AddonName = "ContentsFinder";

    private readonly List<IDutyBadgeSource> sources = [];
    private readonly Dictionary<uint, IconImageNode> badgesByRow = [];

    private NativeListController<AddonContentsFinder, DutyFinderRow>? rows;
    private bool disposed;

    /// <inheritdoc/>
    public IDisposable Mark(IDutyBadgeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(disposed, this);

        _ = framework.RunOnFrameworkThread(() => Begin(source));
        return new Marking(this, source);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await framework.RunOnFrameworkThread(() =>
        {
            sources.Clear();
            FreeAll();
        }).ConfigureAwait(false);

        if (rows is { } watching)
        {
            rows = null;
            await watching.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The row the duty list fills every one of its rows in from. Everything the game
    /// draws in this list goes through it, which is what there is to listen to.</summary>
    private static unsafe AtkComponentListItemRenderer* Template(AddonContentsFinder* addon) =>
        addon == null || addon->DutyList == null
            ? null
            : addon->DutyList->GetComponentItemRendererById(DutyFinderMetrics.RowTemplateNodeId);

    /// <summary>Hangs a new mark off a row, beside its name, or null when there is no name to hang
    /// it beside. Its place is the row's own, which is the space the name is placed in.</summary>
    private static unsafe IconImageNode? Attach(DutyFinderRow row)
    {
        var name = row.NameNode;
        if (name == null)
        {
            return null;
        }

        var badge = new IconImageNode
        {
            Size = new(DutyFinderMetrics.BadgeSize, DutyFinderMetrics.BadgeSize),
            Position = new(DutyFinderMetrics.BadgeLeft, DutyFinderMetrics.BadgeTop),
            IsVisible = false,
        };
        badge.AttachNode(name, NodePosition.AfterTarget);
        return badge;
    }

    /// <summary>Takes up marking on behalf of something, and starts listening to the window if
    /// nothing else was. On the framework thread.</summary>
    private unsafe void Begin(IDutyBadgeSource source)
    {
        if (disposed)
        {
            return;
        }

        sources.Add(source);
        if (sources.Count > 1)
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

    /// <summary>Gives up marking on behalf of something. When it was the last, the marks come off
    /// and the window is let alone: it may still be open, so the nodes go now rather than at a
    /// close that may never come this session. On the framework thread.</summary>
    private void End(IDutyBadgeSource source)
    {
        if (disposed || !sources.Remove(source) || sources.Count > 0)
        {
            return;
        }

        rows?.Disable();
        FreeAll();
    }

    /// <summary>Whether this row wants a mark at all. Answering no here is what has the game tell
    /// us to take an old one back off.</summary>
    private unsafe bool Wants(AddonContentsFinder* addon, DutyFinderRow row) => IconFor(row) is not null;

    /// <summary>The icon this row should carry, or null for none: what the row is for, put to
    /// whoever is marking, first answer wins.</summary>
    private uint? IconFor(DutyFinderRow row)
    {
        if (row.Duty is not { } duty)
        {
            return null;
        }

        foreach (var source in sources)
        {
            if (source.IconFor(duty) is { } icon)
            {
                return icon;
            }
        }

        return null;
    }

    /// <summary>Marks a row the game has just drawn, making the mark if this row has not carried
    /// one before.</summary>
    private unsafe void Set(AddonContentsFinder* addon, DutyFinderRow row)
    {
        try
        {
            if (IconFor(row) is not { } icon)
            {
                return;
            }

            if (!badgesByRow.TryGetValue(row.NodeId, out var badge))
            {
                if (Attach(row) is not { } made)
                {
                    return;
                }

                badge = made;
                badgesByRow[row.NodeId] = badge;
            }

            badge.IconId = icon;
            badge.IsVisible = true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "a Duty Finder row could not be marked, so it stays unmarked.");
        }
    }

    /// <summary>Takes the mark back off a row that no longer wants one. The game says when, which
    /// is the whole reason a row can be handed to another duty without a mark going with it.</summary>
    private unsafe void Clear(AddonContentsFinder* addon, DutyFinderRow row)
    {
        if (badgesByRow.Remove(row.NodeId, out var badge))
        {
            badge.Dispose();
        }
    }

    /// <summary>Frees every mark, and safe to call when there is nothing to free.</summary>
    private void FreeAll()
    {
        foreach (var badge in badgesByRow.Values)
        {
            badge.Dispose();
        }

        badgesByRow.Clear();
    }

    /// <summary>Gives up marking, from wherever the holder happens to let go.</summary>
    private void Release(IDutyBadgeSource source) => _ = framework.RunOnFrameworkThread(() => End(source));

    /// <summary>One thing's marks, taken away when it is disposed.</summary>
    private sealed class Marking(DutyFinderBadges badges, IDutyBadgeSource source) : IDisposable
    {
        private int done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref done, 1) == 0)
            {
                badges.Release(source);
            }
        }
    }
}
