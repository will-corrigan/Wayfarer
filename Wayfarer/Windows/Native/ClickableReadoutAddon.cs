using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
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
/// <b>What it must never do.</b> Steal focus: <c>DisableFocusOnShow</c> and
/// <c>DisableFocusability</c> are both set, which is precisely the pair KamiToolKit sets for its own
/// overlays — the third flag it sets for those, click-through, is the one deliberately left off
/// here. Be dragged: dragging is the window node's header collision, and that node is invisible.
/// Be closed by Esc: <c>RespectCloseAll</c> is off. Make a sound, or offer a title-bar menu: both
/// off.
///
/// <b>Scale.</b> The game renders a normal addon at the player's interface scale, while the body
/// multiplies that scale in by hand (it was written for an overlay, which is de-scaled to raw
/// pixels). Applying the same de-scaling here is what keeps the two hosts identical instead of
/// double-scaled.</summary>
internal sealed unsafe class ClickableReadoutAddon(
    Func<ReadoutFrame?> provider,
    Action onTeleportClicked,
    IFramework framework,
    IPluginLog log) : NativeAddon
{
    private ReadoutBodyNode? body;
    private Vector2 lastSize;
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
        // Neither of these can be set through the toolkit's own surface, and both are what keep a
        // clickable addon from behaving like a window: never take focus when shown, and never be
        // focusable at all. Click dispatch runs through the collision node list, not the focus
        // stack, so events still arrive.
        addon->DisableFocusOnShow = true;
        addon->DisableFocusability = true;

        body = new ReadoutBodyNode(log, onTeleportClicked)
        {
            Position = Vector2.Zero,
        };
        AddNode(body);

        framework.Update += OnFrameworkUpdate;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        framework.Update -= OnFrameworkUpdate;
        body = null;
        lastSize = Vector2.Zero;
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
        var position = ReadoutPlacement.Resolve(frame.Position, size);

        // Only when it actually changed: SetWindowSize writes through to the game's own sizing path,
        // and rebuilding the collision list every frame for an unchanged rectangle is pure waste.
        if (Vector2.DistanceSquared(size, lastSize) > 1f)
        {
            lastSize = size;
            SetWindowSize(size);
            lastHadClickTarget = !body.HasLiveClickTarget;
        }

        RefreshCollision();
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
