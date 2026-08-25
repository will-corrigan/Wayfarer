using Dalamud.Plugin.Services;
using KamiToolKit.ContextMenu;

namespace Wayfarer.Windows.Native;

/// <summary>The list the follow switcher drops: <b>the game's own context menu</b>, opened on demand
/// with our entries in it.
///
/// <para><b>Why this replaced a hand-built dropdown.</b> The previous one was a
/// <c>SimpleNineGridNode</c> on <c>ui/uld/ListB.tex</c> with <c>ListButtonNode</c> rows, on the
/// argument that those are what the game's own drop-downs are made of. Three things were wrong with
/// it, and they were reported in exactly these words: "an ugly grey box", "I can't really scroll on
/// it or pick anything", "that's overlapping my party menu underneath".
/// <list type="number">
/// <item><b>The grey box was the art doing what it says.</b> <c>ListB.tex</c> was extracted and
/// looked at: the whole sheet is 32x64 and contains one plain dark-grey rounded rectangle. There is
/// no list chrome on it to wear. It was never going to look like anything else, and no amount of
/// insets or part ids would have changed that — the premise that it carried the game's list styling
/// was simply false.</item>
/// <item><b>Rows and scrolling.</b> The rows are <c>AtkComponentNode</c>s built by hand inside an
/// overlay addon that is deliberately unfocusable and outside controller navigation, whose collision
/// list we rebuild ourselves. Making component buttons and a scrollbar reliably receive input in
/// that context is a fight against three deliberate properties of the host at once.</item>
/// <item><b>Depth.</b> A plugin addon cannot join the game's HUD depth ordering — there is no
/// registration API, and KamiToolKit explicitly opts out of addon config. A free-floating panel of
/// ours will land on top of, or under, whatever the player happens to have there. That is not a bug
/// to fix; it is what a plugin-drawn panel is.</item>
/// </list></para>
///
/// <para><b>What the game's context menu gives instead, for free.</b> It is the real thing —
/// <c>AgentContext</c> builds it, so it is drawn by the game, at the game's own depth, with the
/// game's own chrome, its own hover and click handling, its own scrolling when there are more
/// entries than fit, its own dismissal on click-away and Escape, and full controller navigation. All
/// three complaints go away at once, and none of it is code we own. It also opens at the cursor,
/// which is where the player just clicked.</para>
///
/// <para>Wayfarer already puts a submenu in this exact menu (<c>ContextMenuActions</c>), so the list
/// a player gets from the caret is the one they already know from right-clicking themselves.</para>
///
/// <para><b>The one thing it costs.</b> The menu is the game's, so its rows are the game's: plain
/// text entries, no right-hand caption column. The Following tab keeps the fuller two-column list —
/// which is what that tab is for — and the caption is folded into the entry's own words here.</para>
///
/// <para>Built lazily on the first open, because the wrapper allocates a native event interface in
/// UI space and that wants the framework thread. Disposed with the host.</para></summary>
internal sealed class FollowSwitcherMenu(IPluginLog log) : IDisposable
{
    /// <summary>What is said if the game refuses to open its menu for us. Losing it costs the
    /// shortcut and nothing else, which is what this says rather than making it sound fatal.</summary>
    private const string Unavailable =
        "Wayfarer readout: the follow list could not be opened, so the caret beside the quest name does nothing "
        + "for the rest of this session. What is being followed is still chosen from the window's Following tab "
        + "and from the Wayfarer entry in the game's own right-click menu.";

    private ContextMenu? menu;
    private bool broken;

    /// <summary>Opens the game's context menu at the cursor with one entry per choice. Choices with
    /// nothing to activate are listed and disabled rather than hidden — the same rule the Following
    /// tab follows, because a choice that vanishes when it is empty cannot be learned.</summary>
    public void Open(IReadOnlyList<FollowChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        if (broken || choices.Count == 0)
        {
            return;
        }

        try
        {
            menu ??= new ContextMenu();
            menu.Clear();

            foreach (var choice in choices)
            {
                menu.AddItem(Entry(choice));
            }

            menu.Open();
        }
        catch (Exception ex)
        {
            broken = true;
            log.Error(ex, Unavailable);
        }
    }

    public void Dispose()
    {
        try
        {
            // Closed before it is disposed, and in that order. Disposing frees the event interface
            // the game holds a pointer to in every menu entry that is on screen, and it calls into
            // that pointer on the next click — closing gives the entries back to the game first.
            // Must therefore run on the framework thread; the caller marshals.
            menu?.Close();
            menu?.Dispose();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Wayfarer readout: disposing the follow list's context menu failed.");
        }

        menu = null;
    }

    /// <summary>One follow choice as a row of the game's menu. Shared with the menu the readout
    /// drops for a controller (<see cref="ReadoutMenu"/>), which lists the same choices in a
    /// submenu: the words, and the rule that a choice with nothing behind it is shown disabled
    /// rather than hidden, are decided once here.</summary>
    internal static ContextMenuItem Entry(FollowChoice choice)
    {
        var activate = choice.Activate;
        return new ContextMenuItem
        {
            Name = Label(choice),
            IsEnabled = activate is not null && !choice.IsFollowed,
            OnClick = activate ?? (() => { }),
        };
    }

    /// <summary>One entry's words. The tab's right-hand caption is folded in after the name, because
    /// a game context-menu row is a single string — and the entry that is already being followed says
    /// so, since the menu has no checked state and a disabled row on its own does not explain
    /// itself.</summary>
    private static string Label(FollowChoice choice)
    {
        if (choice.IsFollowed)
        {
            return $"{choice.Label} (following)";
        }

        return choice.Detail.Length > 0 ? $"{choice.Label} ({choice.Detail})" : choice.Label;
    }
}
