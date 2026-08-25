using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace Wayfarer.Windows.Native;

/// <summary>The readout's host: the <see cref="ReadoutBodyNode"/> in a chromeless native addon that
/// a mouse can click and a controller can reach.
///
/// <b>Why there is one host and not two.</b> There used to be two, chosen by what the player was
/// holding, on the belief that an always-visible focusable window is a trap for a controller — that
/// a cursor could land on it mid-fight. It cannot. The game puts a controller into UI navigation
/// with a deliberate press of its own (<i>HUD Select</i>: the touchpad, the View button, the minus
/// button; <c>PAD_HUDFOCUS</c> in the game's own keybind names) and only then does anything cycle.
/// Nothing about holding a pad moves the cursor onto a window by itself. So the mouse's host became
/// the only host: the same pixels, the same four controls, reachable both ways — plus one press a
/// mouse has no use for, the game's own Display Subcommands on the plate, which drops the rest of
/// Wayfarer's actions in the game's own menu (<see cref="ReadoutMenu"/>).
///
/// <b>Why it does not look like a window.</b> It is chromeless. KamiToolKit builds a
/// <see cref="WindowNode"/> for every non-overlay addon, and this supplies that node already
/// invisible, so the frame, the title bar, the close button and the draggable header are all
/// allocated and none of them are drawn. The readout therefore renders pixel-for-pixel as it did on
/// the overlay — that appearance is a fixed point, and hosting is not allowed to change it.
///
/// <b>What it must never do.</b> Take focus unasked: <c>DisableFocusOnShow</c> is set, so being
/// shown never moves the cursor here — the same posture the game's own HUD elements have, which are
/// never focused until HUD Select reaches them and are reachable all the same. Be closed by Esc:
/// <c>RespectCloseAll</c> is off and <c>DisableUnfocusedCloseOnEsc</c> is on, and if the game closes
/// it anyway <see cref="GuidanceOverlay"/> opens it again on the next frame, so Esc cannot make the
/// readout go away for the session. Make a sound, or offer a title-bar menu: both off. Be dragged
/// other than deliberately: dragging is the window node's header collision, and that node is
/// invisible.
///
/// <b>What it deliberately no longer does.</b> Set <c>DisableFocusability</c> and the "disable
/// controller nav" bit of <c>Flags1A2</c> — the two flags KamiToolKit sets for its own click-through
/// overlays. Those are what kept a pad out, and keeping a pad out was the mistake.
///
/// <b>The two menus it owns.</b> Both are the game's own, opened through <c>AgentContext</c> rather
/// than drawn by us, so their depth, their input, their scrolling and their dismissal are all the
/// game's: the follow switcher's list, dropped by the cap at the plate's right end
/// (<see cref="FollowSwitcherMenu"/> — read it for what that fixed and what it costs), and the
/// readout's own subcommand list, dropped by the plate when a controller asks it for subcommands
/// (<see cref="ReadoutMenu"/>). Both hand memory back to the game, so both are freed on the framework
/// thread; see <see cref="Dispose"/>.
///
/// <b>Scale — and this host now does nothing whatsoever about it.</b> The game renders a normal
/// addon at the player's interface scale, which is exactly what is wanted: the addon that draws the
/// game's own Main Scenario Guide is rendered the same way, so a banner built from the same ULD
/// units cannot come out a different size from it. This used to force
/// <c>SetScale(1 / GetGlobalUIScale())</c> and have the body multiply every dimension back up by
/// <c>GetGlobalUIScale()</c>, on the belief that the two cancelled. They do not — the toolkit's own
/// addon-config code reads a user scale back as <c>InternalAddon-&gt;Scale / GetGlobalUIScale()</c>,
/// so a normally-scaled addon's raw <c>Scale</c> IS <c>GetGlobalUIScale()</c>, and forcing
/// <c>1/g</c> rendered the readout at <c>1/g</c> against the game's own <c>g</c>. Identical only at
/// exactly 100% interface size, and visibly larger below it. That was "it is still bigger than the
/// game's banner".
///
/// The one consequence to keep in mind: <see cref="ReadoutBodyNode.Layout"/> returns a size in
/// addon UNITS here, while <see cref="ReadoutPlacement"/> works in screen PIXELS, so
/// <see cref="Render"/> converts between them.</summary>
internal sealed unsafe class ReadoutAddon(
    Func<ReadoutFrame?> provider,
    ReadoutPlacement placement,
    Action onTeleportClicked,
    Action onDutyClicked,
    Action onSettingsClicked,
    Func<IReadOnlyList<FollowChoice>> getFollowChoices,
    Action onSubjectClicked,
    GuidanceActions actions,
    ITextureProvider textures,
    IFramework framework,
    IPluginLog log,
    Func<bool> diagnosticsEnabled) : NativeAddon
{
    /// <summary>The follow switcher's list. Not a node and not a child of anything here — it asks the
    /// game to open its own context menu, which the game then owns entirely. See
    /// <see cref="FollowSwitcherMenu"/>.</summary>
    private readonly FollowSwitcherMenu followMenu = new(log);

    /// <summary>What a controller's Confirm on the plate opens: the game's own menu, holding every
    /// action on the readout. Same kind of thing as the follow list above, and freed the same way.
    /// See <see cref="ReadoutMenu"/>.</summary>
    private readonly ReadoutMenu actionMenu = new(actions, getFollowChoices, log);

    private ReadoutBodyNode? body;

    private Vector2 lastSize;
    private Vector2 lastPosition;

    /// <summary>The set of clickable nodes the collision list was last built for. Starts at -1,
    /// which no real set can equal, so the first frame always builds one.</summary>
    private int lastClickTargets = -1;

    private bool broken;

    /// <summary>Whether the game had the cursor on this addon last frame — see
    /// <see cref="ReportFocus"/>.</summary>
    private bool hadFocus;

    /// <inheritdoc/>
    public override void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;

        // Same marshalling as the hub window, and for the same reason: Dalamud unloads plugins on a
        // thread-pool thread while Close() asserts the main thread.
        //
        // EVERYTHING that gives memory back to the game belongs below this check, and the two menus
        // are easy to miss: each owns an AtkEventInterface allocated out of the game's UI heap and
        // handed back with two IMemorySpace.Free calls. Freeing that heap from a thread-pool thread
        // is unsynchronised mutation of a structure the game is using — nothing throws, nothing is
        // logged, and the corruption surfaces later somewhere else. The follow menu was disposed
        // four lines above this check until the review that found it.
        if (framework.IsInFrameworkUpdateThread)
        {
            followMenu.Dispose();
            actionMenu.Dispose();
            base.Dispose();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(() =>
            {
                followMenu.Dispose();
                actionMenu.Dispose();
                base.Dispose();
            }).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer readout: disposing the readout on the framework thread failed or timed out, "
                + "so a stray readout may remain on screen until the game is restarted.";
            log.Warning(ex, message);
        }
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        // The readout is furniture that can be operated, which is a particular focus posture and
        // neither of the two obvious ones. It is focusABLE — that is what lets the game's own HUD
        // Select bring the cursor here, and it is why DisableFocusability and the "disable controller
        // nav" bit of Flags1A2 are NOT set, though KamiToolKit sets both for its click-through
        // overlays (NativeAddon.Flags.cs, SetOverlayFlags). It is never focusED of its own accord:
        // DisableFocusOnShow means being shown does not move the cursor here, so a readout that
        // appears while the player is fighting cannot take the d-pad away from them. Every HUD
        // element the game ships has exactly this pair — never focused on show, reachable all the
        // same — which is the precedent for reading the two as independent.
        addon->DisableFocusOnShow = true;

        // Esc, when this is not the focused addon, must not be able to close the readout: it is not
        // a window the player opened and there is no obvious way to get it back. RespectCloseAll,
        // set false at construction, is the toolkit's half of the same guarantee; this is the game's.
        addon->DisableUnfocusedCloseOnEsc = true;

        // The show sound is already silenced by OpenWindowSoundEffectId = 0; this is its hide
        // counterpart. The host is opened and closed automatically as the player changes device,
        // which with a two-second hysteresis can happen many times an hour, and a window chime
        // every time would be the loudest thing about a readout that is meant to be furniture.
        addon->DisableShowHideSoundEffects = true;

        // hostIsHudScaled: this is an ordinary addon, so the game already renders it at the player's
        // interface size — exactly as it renders the addon that draws the game's own Main Scenario
        // Guide. The body therefore lays out in plain ULD units and must not scale them itself. See
        // ReadoutBodyNode.hostIsHudScaled for the arithmetic that was wrong before.
        body = new ReadoutBodyNode(
            log,
            textures,
            diagnosticsEnabled,
            onTeleportClicked,
            onDutyClicked,
            onMoved: delta => placement.MoveTo(lastPosition + delta),
            onSettingsClicked: onSettingsClicked,
            onFollowClicked: OpenFollowMenu,
            onSubjectClicked: onSubjectClicked,
            onPlateSubcommand: actionMenu.Open,
            hostIsHudScaled: true)
        {
            Position = Vector2.Zero,
        };
        AddNode(body);

        // Where the cursor lands when HUD Select reaches this addon. The toolkit's default is the
        // window header's focus node, which here is a node inside an invisible window: a cursor on
        // it would be a cursor on nothing. The plate is the readout's face — Confirm opens the
        // Journal, exactly as a click does, and Display Subcommands drops the whole menu.
        if (body.ControllerFocusNode is { } focus)
        {
            addon->FocusNode = focus;
        }

        framework.Update += OnFrameworkUpdate;
    }

    /// <summary>Runs on the framework thread immediately before the game deallocates the addon.
    ///
    /// <para>This host is closed and opened again whenever the native readout is switched off and on
    /// (and once more if the game ever closes it), and the addon — node tree and all — is built from
    /// scratch on every open. Nothing else frees
    /// the tree: the toolkit's unload-time net skips anything still parented, so dropping the
    /// reference would leave a whole readout behind on every transition. Disposing the body root
    /// takes its children with it and reverses the attach; it is guarded against running twice, so
    /// a second close is a no-op rather than a fault.</para></summary>
    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        framework.Update -= OnFrameworkUpdate;

        try
        {
            // Before the dispose, not as part of it: the drag handle's viewport listener is the one
            // thing NodeBase.Dispose does not clean up — see ReadoutBodyNode.StopMoving.
            body?.StopMoving();
            body?.Dispose();
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer readout: disposing the readout body while closing failed, so its text nodes are leaked "
                + "until the plugin is reloaded. The readout itself keeps working.";
            log.Warning(ex, message);
        }

        body = null;
        lastSize = Vector2.Zero;
        lastPosition = Vector2.Zero;
        lastClickTargets = -1;
        broken = false;
        hadFocus = false;
    }

    /// <summary>What the follow switcher's click does: ask the game to open its own context menu, at
    /// the cursor, with the same list the Following tab shows — see
    /// <see cref="Windows.NativeHubWindow.GetFollowChoices"/> and <see cref="FollowSwitcherMenu"/>.
    ///
    /// <para>There is nothing to toggle. The game owns the menu once it is open, including closing
    /// it, so a second click on the caret is a click outside the menu and dismisses it — which is
    /// what a player expects and what the hand-rolled version had to implement (badly) for
    /// itself.</para></summary>
    private void OpenFollowMenu() => followMenu.Open(getFollowChoices());

    private void OnFrameworkUpdate(IFramework tick)
    {
        if (broken || body is null || InternalAddon is null)
        {
            return;
        }

        try
        {
            Render();
            ReportFocus();
        }
        catch (Exception ex)
        {
            broken = true;
            body.HideAll();
            log.Error(ex, "Wayfarer readout: the readout failed and has switched itself off for this session.");
        }
    }

    /// <summary>Says once, each way, when the game hands the cursor to this addon and when it takes
    /// it back. Only with diagnostics turned on, and only because there is no other way to see it:
    /// whether the game's HUD Select cycle reaches a plugin's own addon is a question about the
    /// game's cursor, and this is the line that answers it from inside the game rather than from
    /// reading its code.</summary>
    private void ReportFocus()
    {
        if (!diagnosticsEnabled())
        {
            return;
        }

        var manager = RaptureAtkUnitManager.Instance();
        var focused = manager is not null && manager->FocusedAddon == InternalAddon;
        if (focused == hadFocus)
        {
            return;
        }

        hadFocus = focused;
        log.Debug(
            focused
                ? "Wayfarer readout: the game has given the readout the cursor — a controller can operate it now."
                : "Wayfarer readout: the game has taken the cursor off the readout.");
    }

    private void Render()
    {
        if (provider() is not { } frame || frame.Content.IsEmpty)
        {
            body!.HideAll();
            RefreshCollision();
            return;
        }

        // In ADDON UNITS — the same units the game's own banner is authored in, because this addon
        // is left at the scale the game gives it. See ReadoutBodyNode.hostIsHudScaled.
        var size = body!.Layout(frame);

        // Only when it actually changed: SetWindowSize writes through to the game's own sizing path,
        // and rebuilding the collision list every frame for an unchanged rectangle is pure waste.
        if (Vector2.DistanceSquared(size, lastSize) > 1f)
        {
            lastSize = size;
            SetWindowSize(size);

            // Force the collision list to be rebuilt below: resizing the host moves every hit box
            // inside it, whether or not the set of them changed.
            lastClickTargets = -1;
        }

        RefreshCollision();

        // Placement is screen pixels — the safe area, the minimap's rectangle and the clamp are all
        // measured in them — so the addon's extent has to be converted out of units first, or a
        // readout at any interface size but 100% would be clamped against a box the wrong size.
        var position = placement.Resolve(size * AtkUnitBase.GetGlobalUIScale());

        // Remembered because a drag is reported as an offset from wherever the host currently is,
        // and the body has no way to ask the addon where that was.
        lastPosition = position;
        SetWindowPosition(position);
    }

    /// <summary>Rebuilds the addon's collision list when the set of clickable nodes changes. The
    /// game dispatches mouse events through that list, so a hit box that appears without this is a
    /// hit box that is never hit.</summary>
    private void RefreshCollision()
    {
        var live = body?.ClickTargets ?? 0;
        if (live == lastClickTargets)
        {
            return;
        }

        lastClickTargets = live;
        InternalAddon->UpdateCollisionNodeList(false);
    }
}
