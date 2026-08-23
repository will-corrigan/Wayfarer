using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using KamiToolKit.Nodes;
using KamiToolKit.UiOverlay;
using Wayfarer.Core.Input;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>Owns the guidance readout and decides which of its two hosts is on screen.
///
/// There is one readout — one <see cref="ReadoutBodyNode"/> definition, one layout pass, one set of
/// fonts and colours — and two ways to host it, chosen by what the player is holding:
///
/// <list type="bullet">
/// <item><description><b>Mouse</b> gets <see cref="ClickableReadoutAddon"/>, a chromeless addon that
/// receives clicks, so "Teleport to X first" is one click. That is the default loop: walk, read,
/// click, teleport.</description></item>
/// <item><description><b>Controller</b> gets <see cref="GuidanceOverlayNode"/>, which is
/// click-through and outside the cursor graph by construction — a focusable surface floating over
/// the world is exactly the thing that traps a controller cursor. The same teleport is a d-pad press
/// away on the game's own context menu and on the window's Quests tab.</description></item>
/// </list>
///
/// The failure modes are what make this safe to do at all: a host that never attaches draws nothing,
/// and "nothing appears" is recoverable. If the clickable addon cannot be created the overlay covers
/// mouse players too — read-only, but there. And whenever the host this player's device would
/// actually use is missing, in either direction, <see cref="IsActive"/> reports false and the ImGui
/// widget takes over: there is no arrangement of failures that leaves a readout on no surface at
/// all.</summary>
internal sealed class GuidanceOverlay(
    ReadoutFeed feed,
    QuestHelperConfig cfg,
    ReadoutPlacement placement,
    InputModeService inputMode,
    IObjectTable objects,
    IClientState clientState,
    IFramework framework,
    ITextureProvider textures,
    IPluginLog log) : IDisposable
{
    private OverlayController? controller;
    private GuidanceOverlayNode? node;
    private ClickableReadoutAddon? clickable;
    private bool started;
    private bool disposed;

    /// <summary>Whether the readout is actually on screen <b>in the host this player's current
    /// device would use</b>. That qualification is the whole of it: asking only whether either host
    /// exists is what let a controller player end up with nothing at all. The clickable host is
    /// closed outside mouse mode by design, so if the overlay failed to construct while the
    /// clickable one succeeded, "a host exists" was true, the ImGui fallback stood down, and the
    /// surface that existed was the one deliberately not being shown.
    ///
    /// <para>The ImGui widget keys off this, so it now takes over in exactly the cases where
    /// nothing native is going to draw — in either mode.</para></summary>
    public bool IsActive => cfg.UseNativeReadout && (UseClickableHost || node is not null);

    // The overlay is the fallback host as well as the controller's host, so it takes over whenever
    // the clickable one is absent for any reason at all.
    private bool UseClickableHost =>
        clickable is not null && cfg.UseNativeReadout && inputMode.Mode == InputMode.Mouse;

    /// <summary>Creates both hosts, marshalling onto the framework thread because every node
    /// constructor and the overlay controller itself assert it. Idempotent.</summary>
    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;

        // Fire and forget: Create() swallows and logs its own failures, and there is nothing for
        // the constructor to do with the task beyond that.
        _ = framework.RunOnFrameworkThread(Create);
    }

    public void Dispose()
    {
        // Set before anything is torn down, so a Create() still sitting in the framework queue
        // sees it and declines rather than building an overlay for a plugin that no longer exists.
        disposed = true;

        framework.Update -= OnFrameworkUpdate;

        clickable?.Dispose();
        clickable = null;

        if (controller is null)
        {
            return;
        }

        // Same marshalling as the native window, and for the same reason: Dalamud unloads plugins
        // on a thread-pool thread while the controller's Dispose asserts the framework thread.
        // Disposing a node off-thread is worse than throwing — it logs nothing and leaks the node.
        var owned = controller;
        var ownedNode = node;
        controller = null;
        node = null;

        // The drag handle's viewport listener has to come down before the node does — NodeBase's
        // own Dispose does not do it. See ReadoutBodyNode.StopMoving.
        void Teardown()
        {
            ownedNode?.StopMoving();
            owned.Dispose();
        }

        if (framework.IsInFrameworkUpdateThread)
        {
            Teardown();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(Teardown).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer readout: disposing the overlay on the framework thread failed or timed out, so a "
                + "stray readout may remain on screen until the game is restarted.";
            log.Warning(ex, message);
        }
    }

    private static unsafe float CameraYaw()
    {
        var cameraManager = CameraManager.Instance();
        return cameraManager != null && cameraManager->Camera != null ? cameraManager->Camera->DirH : 0f;
    }

    /// <summary>Builds both hosts. Queued onto the framework thread by <see cref="Start"/>, which
    /// does not wait for it — so by the time this runs the plugin may already have been unloaded.
    /// Without the guard, that path ends with an overlay controller, a native addon and a live
    /// per-frame subscription all belonging to a plugin that is gone, which is the shape of an
    /// unload crash this plugin has shipped once already.</summary>
    private void Create()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            // Exactly one controller for the plugin's lifetime — a second would duplicate the
            // addon-creation state machine and its per-frame handler.
            controller = new OverlayController();
            node = new GuidanceOverlayNode(
                () => BuildFrame(forClickableHost: false), placement, textures, log, () => cfg.LogDiagnostics);
            controller.AddNode(node);
        }
        catch (Exception ex)
        {
            controller = null;
            node = null;
            const string message =
                "Wayfarer readout: the game-styled readout could not be created, so the plugin-drawn widget is "
                + "being used instead for this session. The same words and the same arrow; it just will not "
                + "match the game's fonts.";
            log.Warning(ex, message);
        }

        CreateClickable();

        // Checked again: Dispose runs on whichever thread Dalamud unloads on, so it can have
        // arrived while the two constructors above were running and found nothing yet to tear
        // down. Undoing it here is what makes the guard at the top of this method total.
        if (disposed)
        {
            Dispose();
            return;
        }

        framework.Update += OnFrameworkUpdate;
    }

    /// <summary>The mouse-only half of <see cref="Create"/>: an addon that can be clicked, as
    /// opposed to the overlay, which is click-through by construction. Failing here costs the
    /// teleport click and nothing else, which is why it is guarded separately.</summary>
    private void CreateClickable()
    {
        try
        {
            clickable = new ClickableReadoutAddon(
                () => BuildFrame(forClickableHost: true),
                placement,
                Teleport,
                textures,
                framework,
                log,
                () => cfg.LogDiagnostics)
            {
                InternalName = "WayfarerReadout",
                Title = "Wayfarer",
                Subtitle = string.Empty,
                CreateWindowNode = () => new WindowNode { NodeId = 2, IsVisible = false },
                EnableContextMenu = false,
                DisableCloseTransition = true,
                RespectCloseAll = false,
                RememberClosePosition = false,
                OpenWindowSoundEffectId = 0,
            };
        }
        catch (Exception ex)
        {
            clickable = null;
            const string message =
                "Wayfarer readout: the clickable readout could not be created, so the readout still shows the "
                + "route but clicking its teleport line will do nothing. Teleport from the window's Quests tab "
                + "or the game's right-click menu instead.";
            log.Warning(ex, message);
        }
    }

    /// <summary>Opens and closes the clickable host as the player changes device. Cheap: two field
    /// reads and a comparison on the frames where nothing has changed.</summary>
    private void OnFrameworkUpdate(IFramework tick)
    {
        if (clickable is null)
        {
            return;
        }

        try
        {
            var wanted = UseClickableHost;
            if (wanted != clickable.IsOpen)
            {
                if (wanted)
                {
                    clickable.Open();
                }
                else
                {
                    clickable.Close();
                }
            }
        }
        catch (Exception ex)
        {
            var failed = clickable;
            clickable = null;
            const string message =
                "Wayfarer readout: the clickable readout failed to open, so the read-only overlay is being used "
                + "for the rest of the session — the readout still shows the route, but its teleport line is no "
                + "longer clickable.";
            log.Warning(ex, message);
            try
            {
                failed.Dispose();
            }
            catch (Exception disposeEx)
            {
                const string disposeMessage =
                    "Wayfarer readout: disposing the failed clickable readout also threw, so an empty readout "
                    + "frame may remain on screen until the game is restarted.";
                log.Warning(disposeEx, disposeMessage);
            }
        }
    }

    private void Teleport()
    {
        if (feed.Navigator.Current.AetheryteId is { } aetheryteId)
        {
            TeleportAction.Execute(aetheryteId, cfg, clientState, log);
        }
    }

    private ReadoutFrame? BuildFrame(bool forClickableHost)
    {
        if (!cfg.UseNativeReadout || !feed.ShouldShow() || forClickableHost != UseClickableHost)
        {
            return null;
        }

        // TeleportOnClick is true only on the host that can actually be clicked, so the readout
        // never promises a click the surface it is drawn on cannot deliver.
        var clickableTeleport = forClickableHost && cfg.ClickTeleportEnabled;
        var content = feed.Compose(teleportOnClick: clickableTeleport);
        var (radians, hidden) = Bearing(content);
        return new ReadoutFrame(
            content,
            radians,
            hidden,
            cfg.ArrowIcon,
            cfg.ArrowScale,
            cfg.TextScale,
            cfg.ReadoutMoveMode,
            clickableTeleport);
    }

    /// <summary>The arrow's rotation, or the reason there isn't one. The reason is carried rather
    /// than discarded because "the arrow is missing" is otherwise indistinguishable from "the arrow
    /// is pointing at nothing" from outside the game — see <see cref="ArrowHiddenReason"/>.
    ///
    /// Note what is deliberately <b>not</b> special-cased here: an objective in another zone still
    /// gets an arrow, because the composer hands back the entrance or aetheryte leg as the target
    /// (<c>ReadoutComposer.AddOtherZone</c>). That was the pre-rewrite widget's behaviour and it is
    /// preserved — the arrow points at the leg you can actually walk.</summary>
    private (float? Radians, ArrowHiddenReason Hidden) Bearing(ReadoutContent content)
    {
        if (!content.ShowArrow)
        {
            return (null, ArrowHiddenReason.NotRequested);
        }

        if (content.TargetX is not { } tx || content.TargetZ is not { } tz)
        {
            return (null, ArrowHiddenReason.NoTargetCoordinates);
        }

        var player = objects.LocalPlayer;
        if (player is null)
        {
            return (null, ArrowHiddenReason.NoPlayer);
        }

        var radians = NavMath.ArrowAngle(
            NavMath.Bearing(tx - player.Position.X, tz - player.Position.Z), CameraYaw());
        return (radians, ArrowHiddenReason.None);
    }
}
