using Dalamud.Plugin.Services;

namespace Wayfarer.App;

/// <summary>Handing work to the game's own thread and away from it, most of all from somewhere that
/// cannot wait for it.
///
/// <para>Most of what Wayfarer does to the game has to happen on the game's thread, and a good deal
/// of it is asked for from somewhere that has nobody to answer to: a button's press, an event
/// Dalamud raises, a command the player typed. There is no caller there to hand the waiting back
/// to, so the work is started and let go of.</para>
///
/// <para>Letting go of it is the part worth being careful about. Work let go of takes any exception
/// with it -- no line in the log, no crash, nothing at all, just a thing that quietly did not
/// happen. That is how a window came to be hooked off the game's thread and say so to nobody. Work
/// handed over through here is let go of just the same, but says what it was trying to do if it
/// fails.</para></summary>
internal static class GameThread
{
    /// <summary>Hands work to the game's thread and lets go of it.</summary>
    /// <param name="framework">The game's own thread.</param>
    /// <param name="work">What to do on it.</param>
    /// <param name="log">Where a failure is written.</param>
    /// <param name="what">What the work was for, in the words of whoever asked for it, so the log
    /// says which of these failed and not merely that one did.</param>
    public static void Hand(this IFramework framework, Action work, IPluginLog log, string what) =>
        Let(() => framework.RunOnFrameworkThread(work), log, what);

    /// <summary>Runs work that has to be on the game's thread, and hands the wait back. Once the
    /// game is shutting down no frame will come to run it, so it is let go of rather than waited on
    /// for ever: nothing a module does to guidance or its own state matters by then.</summary>
    /// <param name="framework">The game's own thread.</param>
    /// <param name="work">What to do on it.</param>
    public static Task OnTheGameThread(this IFramework framework, Action work) =>
        framework.IsFrameworkUnloading ? Task.CompletedTask : framework.RunOnFrameworkThread(work);

    /// <summary>Runs work where it cannot cost a frame, such as reading sheets: here when already
    /// off the game's thread, on the pool when on it.</summary>
    /// <param name="framework">The game's own thread.</param>
    /// <param name="work">What to do off it.</param>
    public static Task OffTheGameThread(this IFramework framework, Action work)
    {
        if (framework.IsInFrameworkUpdateThread)
        {
            return Task.Run(work);
        }

        work();
        return Task.CompletedTask;
    }

    /// <summary>Starts work that is already being done elsewhere and lets go of it.</summary>
    /// <inheritdoc cref="Hand(IFramework, Action, IPluginLog, string)"/>
    public static void Let(Func<Task> work, IPluginLog log, string what) =>
        _ = Watched(work, log, what);

    /// <summary>What actually holds the work long enough to see how it ended. Nothing here is ever
    /// waited for, so it catches everything: an exception escaping this would go on to become one
    /// nobody is holding, which is what the rest of this is for avoiding.</summary>
    private static async Task Watched(Func<Task> work, IPluginLog log, string what)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"could not {what}.");
        }
    }
}
