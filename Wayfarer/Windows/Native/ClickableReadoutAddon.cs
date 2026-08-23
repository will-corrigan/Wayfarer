using System.Numerics;
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
/// <b>Scale.</b> The game renders a normal addon at the player's interface scale, while the body
/// multiplies that scale in by hand (it was written for an overlay, which is de-scaled to raw
/// pixels). Applying the same de-scaling here is what keeps the two hosts identical instead of
/// double-scaled.</summary>
internal sealed unsafe class ClickableReadoutAddon(
    Func<ReadoutFrame?> provider,
    ReadoutPlacement placement,
    Action onTeleportClicked,
    ITextureProvider textures,
    IFramework framework,
    IPluginLog log) : NativeAddon
{
    private ReadoutBodyNode? body;
    private Vector2 lastSize;
    private Vector2 lastPosition;
    private bool lastHadClickTarget;
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
            log.Warning(ex, "Wayfarer readout: disposing the clickable readout on the framework thread failed or timed out.");
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
            onTeleportClicked,
            onMoved: delta => placement.MoveTo(lastPosition + delta))
        {
            Position = Vector2.Zero,
        };
        AddNode(body);

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
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Wayfarer readout: disposing the readout body while closing failed.");
        }

        body = null;
        lastSize = Vector2.Zero;
        lastPosition = Vector2.Zero;
        lastHadClickTarget = false;
        broken = false;
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
        }
        catch (Exception ex)
        {
            broken = true;
            body.HideAll();
            log.Error(ex, "Wayfarer readout: the clickable readout failed and has switched itself off for this session.");
        }
    }

    private void Render()
    {
        ApplyRawPixelScale();

        if (provider() is not { } frame || frame.Content.IsEmpty)
        {
            body!.HideAll();
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
            lastHadClickTarget = !body.HasLiveClickTarget;
        }

        RefreshCollision();

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
        var live = body is { HasLiveClickTarget: true };
        if (live == lastHadClickTarget)
        {
            return;
        }

        lastHadClickTarget = live;
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
