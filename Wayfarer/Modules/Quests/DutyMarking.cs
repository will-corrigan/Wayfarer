using Dalamud.Plugin.Services;

namespace Wayfarer.Modules.Quests;

/// <summary>Holds the Duty Finder's marks up and puts them down, so the module has one thing to
/// switch and nothing to know about how they are hung.
///
/// <para>The marks hook the window and hang nodes on rows that are not ours, so they exist only
/// while they are wanted: switching them off lets go of every one and unhooks, rather than leaving
/// something watching a window nobody asked it to watch.</para>
///
/// <para>Both hooking the window and letting go of a node have to happen on the game's own thread,
/// and a module is brought up and taken down on whichever thread it pleases, so both are handed
/// there rather than done where they were asked for.</para></summary>
internal sealed class DutyMarking(QuestDuties duties, IFramework framework) : IAsyncDisposable
{
    private DutyFinderMarks? marks;

    /// <summary>Starts marking, or does nothing when already marking. Hooking the window has to
    /// happen on the game's own thread, so the caller is handed back the wait for it: a caller that
    /// dropped it could ask to stop before the hook had been made, and the hook would then be made
    /// on a window nobody was watching any more, holding nodes nothing would ever let go of.</summary>
    public Task StartAsync() => framework.RunOnFrameworkThread(Hook);

    /// <summary>Stops marking and lets go of everything hung on the window.</summary>
    public Task StopAsync()
    {
        if (marks is not { } owned)
        {
            return Task.CompletedTask;
        }

        marks = null;
        return framework.RunOnFrameworkThread(owned.Dispose);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private void Hook() => marks ??= new DutyFinderMarks(duties);
}
