using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace Wayfarer.Windows.Native;

/// <summary>The mouse player's host for the guidance readout: the same
/// <see cref="ReadoutBodyNode"/> the overlay draws, in an addon that can be clicked.
///
/// <b>Why this exists.</b> The default loop is walk, read, click, teleport. The readout says
/// "Teleport to Horizon first" and, on the overlay, that line cannot be clicked — an overlay is
/// click-through by construction, which is exactly what makes it safe for a controller. So a mouse
/// gets a different <i>host</i> for the identical <i>body</i>: one node tree, one layout pass, one
/// set of fonts and colours, hosted in something that receives mouse events.
///
/// <b>Why it does not look like a window.</b> It is chromeless. KamiToolKit builds a
/// <see cref="WindowNode"/> for every non-overlay addon, and this supplies that node already
/// invisible, so the frame, the title bar, the close button and the draggable header are all
/// allocated and none of them are drawn. The readout therefore renders pixel-for-pixel as it does
/// on the overlay — that appearance is a fixed point, and hosting is not allowed to change it.
///
/// <b>What it must never do.</b> Steal focus, or be reachable by a controller:
/// <c>DisableFocusOnShow</c>, <c>DisableFocusability</c> and the "disable controller nav" bit of
/// <c>Flags1A2</c> are all set — three of the four flags KamiToolKit sets for its own overlays.
/// Only the fourth, click-through, is deliberately left off, because being clickable is the entire
/// point of this host. Be dragged: dragging is the window node's header collision, and that node is
/// invisible. Be closed by Esc: <c>RespectCloseAll</c> is off. Make a sound, or offer a title-bar
/// menu: both off.
///
/// <b>The settings cog.</b> The readout carries one, and only here. The player asked to be able to
/// reach Settings from the readout instead of going through the plugin list, and this host is the one
/// that can receive the click. The overlay deliberately does not draw one — see
/// <see cref="ReadoutBodyNode"/>.
///
/// <b>The follow switcher's dropdown.</b> Also only here, and for the same reason as the cog — a
/// controller's readout is click-through by construction and cannot host anything interactive at
/// all, let alone a popup list. <see cref="popup"/> is owned by this addon rather than by the body,
/// toggled by the switcher's click, and closes on a click anywhere else, on Escape, or on picking a
/// row — see <see cref="FollowSwitcherPopupNode"/>.
///
/// <b>Scale.</b> The game renders a normal addon at the player's interface scale, while the body
/// multiplies that scale in by hand (it was written for an overlay, which is de-scaled to raw
/// pixels). Applying the same de-scaling here is what keeps the two hosts identical instead of
/// double-scaled.</summary>
internal sealed unsafe class ClickableReadoutAddon(
    Func<ReadoutFrame?> provider,
    ReadoutPlacement placement,
    Action onTeleportClicked,
    Action onSettingsClicked,
    Func<IReadOnlyList<FollowChoice>> getFollowChoices,
    Action onQuestNameClicked,
    ITextureProvider textures,
    IFramework framework,
    IKeyState keyState,
    IPluginLog log,
    Func<bool> diagnosticsEnabled) : NativeAddon
{
    /// <summary>Bit added to <see cref="lastClickTargets"/> while the follow switcher's dropdown is
    /// open — its own rows and scrollbar are collision nodes that come and go with it, same
    /// reasoning as <see cref="ReadoutBodyNode.ClickTargets"/>'s own bits. Deliberately outside that
    /// range (1/2/4/8) rather than adding a fifth bit there: the dropdown is this host's own state,
    /// not the body's — see <see cref="popup"/>.</summary>
    private const int PopupOpenTarget = 16;

    private ReadoutBodyNode? body;

    /// <summary>The follow switcher's dropdown — a sibling of <see cref="body"/>, not a part of it.
    /// See <see cref="FollowSwitcherPopupNode"/>'s own doc comment for why it lives here rather than
    /// in the body shared with the click-through overlay.</summary>
    private FollowSwitcherPopupNode? popup;

    private Vector2 lastSize;
    private Vector2 lastPosition;

    /// <summary>The set of clickable nodes the collision list was last built for. Starts at -1,
    /// which no real set can equal, so the first frame always builds one.</summary>
    private int lastClickTargets = -1;

    /// <summary>Edge-detects Escape for the dropdown. This addon is deliberately outside the game's
    /// own focus stack (<c>DisableFocusability</c> — see the class doc comment), so it cannot rely
    /// on the native "Escape closes the focused popup" behaviour a real window gets for free, and
    /// polls the key itself instead, only while the dropdown is actually open.</summary>
    private bool escapeWasDown;

    private bool broken;

    /// <inheritdoc/>
    public override void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;

        // Same marshalling as the hub window, and for the same reason: Dalamud unloads plugins on a
        // thread-pool thread while Close() asserts the main thread.
        if (framework.IsInFrameworkUpdateThread)
        {
            base.Dispose();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(() => base.Dispose()).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer readout: disposing the clickable readout on the framework thread failed or timed out, "
                + "so a stray readout may remain on screen until the game is restarted.";
            log.Warning(ex, message);
        }
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        // None of these can be set through the toolkit's own surface, and together they are what
        // keep a clickable addon from behaving like a window: never take focus when shown, never be
        // focusable at all, and never appear in the controller's navigation graph. Click dispatch
        // runs through the collision node list, not the focus stack, so mouse events still arrive.
        //
        // The third is the one this surface would otherwise be missing. KamiToolKit sets all three
        // together for its own non-interactive addons (NativeAddon.Flags.cs, SetOverlayFlags), but
        // only reaches that path for overlays, and this is a normal addon. Nothing here is ever
        // meant to be reached by a d-pad — the same teleport is on the game's context menu and on
        // the window's Quests tab — so being outside the graph costs nothing and closes the one
        // question this addon's focus posture could not otherwise answer: a player who pins the
        // input mode to Mouse while holding a pad would otherwise have a focusable surface parked
        // over the world with a cursor able to land on it.
        addon->DisableFocusOnShow = true;
        addon->DisableFocusability = true;
        FlagHelper.UpdateFlag(ref addon->Flags1A2, 0x2, true);

        // The show sound is already silenced by OpenWindowSoundEffectId = 0; this is its hide
        // counterpart. The host is opened and closed automatically as the player changes device,
        // which with a two-second hysteresis can happen many times an hour, and a window chime
        // every time would be the loudest thing about a readout that is meant to be furniture.
        addon->DisableShowHideSoundEffects = true;

        body = new ReadoutBodyNode(
            log,
            textures,
            diagnosticsEnabled,
            onTeleportClicked,
            onMoved: delta => placement.MoveTo(lastPosition + delta),
            onSettingsClicked: onSettingsClicked,
            onFollowClicked: ToggleFollowSwitcher,
            onQuestNameClicked: onQuestNameClicked)
        {
            Position = Vector2.Zero,
        };
        AddNode(body);

        // Attached after the body, so its rows and its outside-click coverage sit above the
        // body's own controls in hit-test order while open — the same ordering KamiToolKit's own
        // DropDownNode uses between its popup and the collision node that dismisses it. Built
        // empty and toggled open by the switcher; see ToggleFollowSwitcher.
        popup = new FollowSwitcherPopupNode();
        AddNode(popup);

        framework.Update += OnFrameworkUpdate;
    }

    /// <summary>Runs on the framework thread immediately before the game deallocates the addon.
    ///
    /// <para>This host is opened and closed automatically as the player changes device, and the
    /// addon — node tree and all — is built again from scratch on every open. Nothing else frees
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
            popup?.Dispose();
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer readout: disposing the readout body while closing failed, so its text nodes are leaked "
                + "until the plugin is reloaded. The readout itself keeps working.";
            log.Warning(ex, message);
        }

        body = null;
        popup = null;
        lastSize = Vector2.Zero;
        lastPosition = Vector2.Zero;
        lastClickTargets = -1;
        escapeWasDown = false;
        broken = false;
    }

    /// <summary>The full screen, in the same raw pixels this addon's own content already lives in
    /// once <see cref="ApplyRawPixelScale"/> has run. Read fresh every frame rather than cached: a
    /// resolution change is exactly the kind of thing a dropdown's dismiss coverage has to keep up
    /// with immediately, not on whatever frame next happens to touch it.</summary>
    private static Vector2 ScreenSize()
    {
        var stage = AtkStage.Instance();
        return stage is null ? Vector2.Zero : new Vector2(stage->ScreenSize.Width, stage->ScreenSize.Height);
    }

    /// <summary>What the follow switcher's click does: close the dropdown if it is already open,
    /// otherwise open it at the switcher's current position with the same list the Following tab
    /// shows — see <see cref="FollowSwitcherPopupNode"/> and
    /// <see cref="Windows.NativeHubWindow.GetFollowChoices"/>. Nothing opens a window any more; the
    /// caret's whole job is this dropdown.</summary>
    private void ToggleFollowSwitcher()
    {
        if (popup is null || body is null)
        {
            return;
        }

        if (popup.IsOpen)
        {
            popup.Close();
            return;
        }

        if (body.DropdownAnchor is { } anchor)
        {
            popup.Open(getFollowChoices(), anchor, body.DropdownWidth);
        }
    }

    private void OnFrameworkUpdate(IFramework tick)
    {
        if (broken || body is null || InternalAddon is null)
        {
            return;
        }

        try
        {
            Render();
            PollEscape();
        }
        catch (Exception ex)
        {
            broken = true;
            body.HideAll();
            log.Error(ex, "Wayfarer readout: the clickable readout failed and has switched itself off for this session.");
        }
    }

    /// <summary>Closes the follow switcher's dropdown on Escape — see <see cref="escapeWasDown"/>
    /// for why this addon has to poll rather than being told. Edge-detected so holding the key down
    /// does not matter, and read only while the dropdown is actually open, which costs nothing on
    /// every other frame of a session that may never open it at all.</summary>
    private void PollEscape()
    {
        if (popup is not { IsOpen: true })
        {
            escapeWasDown = false;
            return;
        }

        var down = keyState[VirtualKey.ESCAPE];
        if (down && !escapeWasDown)
        {
            popup.Close();
        }

        escapeWasDown = down;
    }

    private void Render()
    {
        ApplyRawPixelScale();

        if (provider() is not { } frame || frame.Content.IsEmpty)
        {
            body!.HideAll();
            popup?.Close();
            RefreshCollision();
            return;
        }

        var size = body!.Layout(frame);
        var position = placement.Resolve(size);

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

        // Remembered because a drag is reported as an offset from wherever the host currently is,
        // and the body has no way to ask the addon where that was.
        lastPosition = position;
        SetWindowPosition(position);

        // The dropdown's own outside-click coverage has to track where this addon actually is on
        // screen every frame it is open, because the readout — and the addon under it — can be
        // dragged while the dropdown is up. See FollowSwitcherPopupNode.Reposition.
        if (popup is { IsOpen: true })
        {
            popup.Reposition(position, ScreenSize());
        }
    }

    /// <summary>Rebuilds the addon's collision list when the set of clickable nodes changes. The
    /// game dispatches mouse events through that list, so a hit box that appears without this is a
    /// hit box that is never hit.</summary>
    private void RefreshCollision()
    {
        var live = (body?.ClickTargets ?? 0) | (popup is { IsOpen: true } ? PopupOpenTarget : 0);
        if (live == lastClickTargets)
        {
            return;
        }

        lastClickTargets = live;
        InternalAddon->UpdateCollisionNodeList(false);
    }

    /// <summary>Undoes the interface scale the game applies to a normal addon, so one addon unit is
    /// one screen pixel — the frame of reference <see cref="ReadoutBodyNode"/> and
    /// <see cref="ReadoutPlacement"/> are both written in. Re-applied when it drifts, because the
    /// game rewrites the scale on a resolution or interface-size change.</summary>
    private void ApplyRawPixelScale()
    {
        var target = 1.0f / Math.Max(AtkUnitBase.GetGlobalUIScale(), 0.1f);
        if (Math.Abs(InternalAddon->Scale - target) > 0.001f)
        {
            InternalAddon->SetScale(target, true);
        }
    }
}
