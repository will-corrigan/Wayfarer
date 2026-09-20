using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Wayfarer.App;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Puts a small icon on Duty Finder rows, on behalf of anything that asks. This knows
/// nothing about what the marks mean: it finds the rows, asks whoever is marking what icon each
/// duty should carry, and draws that.
///
/// <para>The window is only watched while something is marking it. A mark hangs off the row that
/// draws it, and the game hands those rows round as the list scrolls, so a mark belongs to a row
/// rather than to a duty: every update, each row on show is asked what it is now for and its mark
/// is set to suit, or hidden when it is for nothing.</para>
///
/// <para>Only the rows the game says it is drawing this frame are touched. A row it has stopped
/// drawing keeps whatever mark it had, which is not drawn either, and is set right again the
/// moment the row comes back — so nothing here ever writes to a row on the strength of having
/// seen it in an earlier frame.</para>
///
/// <para>Every node made here is freed when the window closes, when the last mark is taken away,
/// or when the plugin unloads, whichever comes first. The rows outlive the nodes hung off them in
/// all three cases, so the game is never left holding one that has been freed.</para>
///
/// <para>Who is marking is only ever changed on the framework thread. Modules come up and go down
/// away from it, and the list of them is read while the window draws, which is on it.</para></summary>
internal sealed class DutyFinderBadges(IFramework framework, IPluginLog log) : IDutyBadges, IAsyncDisposable
{
    private static readonly string AddonName = GameAddon.NameOf<AddonContentsFinder>();

    private readonly List<IDutyBadgeSource> sources = [];
    private readonly Dictionary<nint, IconImageNode> badgesByRow = [];
    private readonly DutyRoster roster = new();

    private AddonController? controller;
    private bool disposed;
    private bool broken;

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

        if (controller is { } watching)
        {
            controller = null;
            await watching.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The list of duties, or null when the window has not drawn one.</summary>
    private static unsafe AtkComponentTreeList* ListOf(AtkUnitBase* addon)
    {
        var finder = (AddonContentsFinder*)addon;
        return finder == null ? null : finder->DutyList;
    }

    /// <summary>The words a row is showing, or null when the row is not a duty's. Only a duty's row
    /// has a name node, so a heading answers null here and is passed over without the kinds of
    /// rows ever being enumerated.</summary>
    private static unsafe string? NameOn(AtkComponentListItemRenderer* row)
    {
        var text = row == null
            ? null
            : GameNodes.Text(&row->AtkComponentButton.AtkComponentBase, DutyFinderMetrics.RowNameTextNodeId);
        if (text == null || text->NodeText.Length == 0)
        {
            return null;
        }

        var name = text->NodeText.ExtractText();
        return name.Length > 0 ? name : null;
    }

    /// <summary>Takes up marking on behalf of something, and starts watching the window if nothing
    /// else was. On the framework thread.</summary>
    private unsafe void Begin(IDutyBadgeSource source)
    {
        if (disposed)
        {
            return;
        }

        sources.Add(source);
        if (sources.Count == 1)
        {
            // Marking switched off after it broke and then back on is a fresh ask, not the same
            // one carrying on, so whatever went wrong is given another chance to not.
            broken = false;
            controller ??= new AddonController
            {
                AddonName = AddonName,
                OnFinalize = WindowClosed,
                OnUpdate = Refresh,
            };
            controller.Enable();
        }
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

        FreeAll();
        controller?.Disable();
    }

    /// <summary>Sets every row on show to the mark it should be carrying this frame.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (broken)
        {
            return;
        }

        try
        {
            var list = ListOf(addon);
            if (list == null)
            {
                return;
            }

            roster.Reread();
            foreach (var entry in list->Items)
            {
                var item = entry.Value;
                if (item != null && item->Renderer != null && !item->IsHidden)
                {
                    Show(item->Renderer, WantedOn(item->Renderer));
                }
            }
        }
        catch (Exception ex)
        {
            // Whatever this was will still be true next frame, so marking stops rather than
            // throwing behind a log line once per frame for as long as the window is open. The
            // window is left watched: this is running inside its own update, and tearing that
            // down from in here would free what is calling us.
            broken = true;
            log.Error(ex, "marking the Duty Finder's rows threw, so the marks are switched off for this session.");
            FreeAll();
        }
    }

    /// <summary>The icon a row should carry, or null for none: what the row is for, put to
    /// whoever is marking, first answer wins.</summary>
    private unsafe uint? WantedOn(AtkComponentListItemRenderer* row)
    {
        if (NameOn(row) is not { } name || roster.DutyNamed(name) is not { } duty)
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

    /// <summary>Puts a mark on a row, makes one if the row has not had one before, and hides it
    /// when the row is for nothing that wants marking.</summary>
    private unsafe void Show(AtkComponentListItemRenderer* row, uint? icon)
    {
        if (!badgesByRow.TryGetValue((nint)row, out var badge))
        {
            if (icon is null)
            {
                // Nothing to show and nothing made yet: a row that never needs a mark never gets
                // a node, so a list of a hundred duties with two marks holds two nodes.
                return;
            }

            if (Attach(row) is not { } made)
            {
                return;
            }

            badge = made;
            badgesByRow[(nint)row] = badge;
        }

        if (icon is { } id)
        {
            badge.IconId = id;
        }

        badge.IsVisible = icon is not null;
    }

    /// <summary>Hangs a new mark off a row, or null when the game will not take it.</summary>
    private unsafe IconImageNode? Attach(AtkComponentListItemRenderer* row)
    {
        var owner = row == null ? null : row->OwnerNode;
        if (owner == null)
        {
            return null;
        }

        try
        {
            var badge = new IconImageNode
            {
                Size = new(DutyFinderMetrics.BadgeSize, DutyFinderMetrics.BadgeSize),
                Position = new(DutyFinderMetrics.BadgeLeft, DutyFinderMetrics.BadgeTop),
                IsVisible = false,
            };
            badge.AttachNode((AtkResNode*)owner);
            return badge;
        }
        catch (Exception ex)
        {
            log.Error(ex, "a Duty Finder row would not take a mark, so that row stays unmarked.");
            return null;
        }
    }

    /// <summary>The window has closed and taken its rows with it, so the marks hung off them are
    /// done with too.</summary>
    private unsafe void WindowClosed(AtkUnitBase* addon) => FreeAll();

    /// <summary>Frees every mark. Called when the window closes, when the last thing marking it
    /// goes away, and at unload, and safe to call when there is nothing to free.</summary>
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
