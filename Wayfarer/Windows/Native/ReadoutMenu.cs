using Dalamud.Plugin.Services;
using KamiToolKit.ContextMenu;

namespace Wayfarer.Windows.Native;

/// <summary>What the readout's plate answers when it is asked for subcommands: <b>the game's own
/// context menu</b>, holding everything Wayfarer can be asked to do.
///
/// <para><b>What this is not.</b> It is not how a controller reaches the readout's four controls —
/// those it reaches directly, cycling to each with the d-pad and confirming, exactly as a mouse
/// clicks them. This is the extra: on the plate, the game's own <b>Display Subcommands</b> press
/// opens the whole list, the way that press opens the list of everything that can be done to any
/// other focused thing in this game. Confirm on the plate still opens the Journal, as a click on it
/// does.</para>
///
/// <para><b>Why it is worth having as well.</b> The four controls are the four things the readout
/// itself is about; the list holds the rest — the unlocks window, the hunting log, starting a hunt,
/// stopping one — which a controller otherwise reaches only by right-clicking the world. From the
/// plate it is two presses.</para>
///
/// <para><b>Why the game's menu and not a panel of ours.</b> The same three reasons the follow
/// switcher's list uses it — see <see cref="FollowSwitcherMenu"/>, which learned them the hard way:
/// a plugin-drawn panel cannot join the game's depth ordering, its hand-built rows fight the host's
/// input posture, and there is no list art in the game's textures to wear. <c>AgentContext</c> gives
/// the real thing, navigable with a pad for free.</para>
///
/// <para><b>What is in it.</b> Everything: the Journal, a Follow submenu holding every followable
/// thing, the teleport the readout is recommending, the duty its objective is inside, Stop, the two
/// ways to start something, the unlocks list, the hunting log and Settings. All of it from
/// <see cref="GuidanceActions"/> and
/// <c>NativeHubWindow.GetFollowChoices</c> — the same sources the game's own right-click menu and
/// the window's Following tab read, so the three cannot come to disagree. Entries that do not apply
/// are absent rather than greyed, which is the rule those sources already followed.</para>
///
/// <para>Built lazily on the first open, because the wrapper allocates a native event interface in
/// UI space and that wants the framework thread. Disposed with the host, closed before it is
/// freed.</para></summary>
internal sealed class ReadoutMenu(
    GuidanceActions actions, Func<IReadOnlyList<FollowChoice>> getFollowChoices, IPluginLog log) : IDisposable
{
    /// <summary>What is said if the game refuses to open its menu for us. Losing it costs the
    /// controller's route to the readout's actions and nothing else, so it says where the rest of
    /// them still are rather than sounding fatal.</summary>
    private const string Unavailable =
        "Wayfarer readout: the readout's subcommand menu could not be opened, so asking the readout for subcommands "
        + "does nothing for the rest of this session. The readout's own controls still work, and the same actions "
        + "are all on the Wayfarer entry in the game's own right-click menu.";

    private ContextMenu? menu;
    private bool broken;

    /// <summary>Opens the menu at the cursor. Rebuilt from live state on every open — a menu opened
    /// in one zone and confirmed in another must not act on the first one.</summary>
    public void Open()
    {
        if (broken)
        {
            return;
        }

        try
        {
            menu ??= new ContextMenu();
            menu.Clear();

            // The Journal first: it is what the plate itself does, so the list it drops opens with
            // the same thing rather than burying it.
            Add(actions.Journal());
            AddFollowSubmenu();

            foreach (var action in actions.Route())
            {
                Add(action);
            }

            foreach (var action in actions.Windows())
            {
                Add(action);
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
            log.Warning(ex, "Wayfarer readout: disposing the readout's context menu failed.");
        }

        menu = null;
    }

    /// <summary>"Follow" — the same word, the same choices and the same rows the switcher cap drops
    /// for a mouse, one level down so a long list of accepted quests does not bury the actions
    /// beneath it. One level is also all the game's menu supports.</summary>
    private void AddFollowSubmenu()
    {
        var choices = getFollowChoices();
        if (choices.Count == 0)
        {
            return;
        }

        var submenu = new ContextMenuSubItem
        {
            Name = "Follow",
            OnClick = () => { },
        };

        foreach (var choice in choices)
        {
            submenu.AddItem(FollowSwitcherMenu.Entry(choice));
        }

        menu!.AddItem(submenu);
    }

    private void Add(GuidanceAction? action)
    {
        if (action is not null)
        {
            menu!.AddItem(action.Label, action.Invoke);
        }
    }
}
