using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using KamiToolKit.Nodes;
using KamiToolKit.UiOverlay;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>Owns the guidance readout: one host for every player, and one fallback for when that
/// host cannot be built.
///
/// <b>The host</b> is <see cref="ReadoutAddon"/>, a chromeless native addon that takes input. A
/// mouse clicks its four controls; a controller reaches the same four by pressing the game's own HUD
/// Select and cycling to the readout, which is the interaction the game already teaches for its own
/// HUD. There is no longer a device-dependent host: the readout used to switch to a click-through
/// overlay whenever a pad was in hand, on the belief that a focusable surface over the world would
/// trap the pad's cursor. It cannot — the cursor only moves into the UI when the player asks it to —
/// and the cost of that belief was a controller player who could see the cog and could not press it.
///
/// <b>The fallback</b> is <see cref="GuidanceOverlayNode"/>, and it is now only that. If the addon
/// cannot be created, the overlay draws the identical readout with nothing on it that can be
/// operated, and the log says so once; the game's own right-click menu still reaches the Journal,
/// Settings, the follow list and the teleport. Below that again, if neither native host exists,
/// <see cref="IsActive"/> reports false and the ImGui widget takes over. There is no arrangement of
/// failures that leaves the readout on no surface at all.</summary>
internal sealed class GuidanceOverlay(
    ReadoutFeed feed,
    QuestHelperConfig cfg,
    ReadoutPlacement placement,
    IObjectTable objects,
    IClientState clientState,
    IFramework framework,
    ITextureProvider textures,
    Action onSettingsClicked,
    Func<IReadOnlyList<FollowChoice>> getFollowChoices,
    IPluginLog log) : IDisposable
{
    private OverlayController? controller;
    private GuidanceOverlayNode? node;
    private ReadoutAddon? addon;
    private bool started;
    private bool disposed;

    /// <summary>Whether the readout is on screen on some native surface. The ImGui widget keys off
    /// this, so it takes over in exactly the cases where nothing native is going to draw.</summary>
    public bool IsActive => cfg.UseNativeReadout && (UseAddonHost || node is not null);

    // The overlay is the fallback, so the addon is the host whenever it exists at all.
    private bool UseAddonHost => addon is not null && cfg.UseNativeReadout;

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

        addon?.Dispose();
        addon = null;

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

    /// <summary>Builds the host and its fallback. Queued onto the framework thread by <see cref="Start"/>, which
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
                () => BuildFrame(forAddonHost: false), placement, textures, log, () => cfg.LogDiagnostics);
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

        CreateAddon();

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

    /// <summary>The half of <see cref="Create"/> that builds the host the player actually operates.
    /// Failing here costs every control on the readout and nothing else — the words and the arrow
    /// are the overlay's — which is why it is guarded separately.</summary>
    private void CreateAddon()
    {
        try
        {
            addon = new ReadoutAddon(
                () => BuildFrame(forAddonHost: true),
                placement,
                Teleport,
                onSettingsClicked,
                getFollowChoices,
                OpenJournal,
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
            addon = null;
            const string message =
                "Wayfarer readout: the readout's own host could not be created, so the read-only overlay is being "
                + "used instead — the readout still shows the route, but nothing on it can be pressed. The "
                + "Journal, Settings, the follow list and the teleport are all on the game's right-click menu.";
            log.Warning(ex, message);
        }
    }

    /// <summary>Keeps the host open for as long as the native readout is switched on — and opens it
    /// again if anything else ever closes it, which is what makes Esc harmless. Cheap: two field
    /// reads and a comparison on the frames where nothing has changed.</summary>
    private void OnFrameworkUpdate(IFramework tick)
    {
        if (addon is null)
        {
            return;
        }

        try
        {
            var wanted = UseAddonHost;
            if (wanted != addon.IsOpen)
            {
                if (wanted)
                {
                    addon.Open();
                }
                else
                {
                    addon.Close();
                }
            }
        }
        catch (Exception ex)
        {
            var failed = addon;
            addon = null;
            const string message =
                "Wayfarer readout: the readout's own host failed to open, so the read-only overlay is being used "
                + "for the rest of the session — the readout still shows the route, but nothing on it can be "
                + "pressed. Use the game's right-click menu instead.";
            log.Warning(ex, message);
            try
            {
                failed.Dispose();
            }
            catch (Exception disposeEx)
            {
                const string disposeMessage =
                    "Wayfarer readout: disposing the failed readout host also threw, so an empty readout "
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

    /// <summary>Opens the game's own Journal at whatever is being followed right now. Read off the
    /// live snapshot at the moment of the click rather than captured when the readout was drawn,
    /// exactly as the teleport above is: the readout is a view of that snapshot and the snapshot is
    /// the only thing that knows which quest the name belongs to.</summary>
    private void OpenJournal()
    {
        if (feed.Navigator.Current.QuestId is { } questId)
        {
            QuestJournalAction.Execute(questId);
        }
    }

    private ReadoutFrame? BuildFrame(bool forAddonHost)
    {
        if (!cfg.UseNativeReadout || !feed.ShouldShow() || forAddonHost != UseAddonHost)
        {
            return null;
        }

        // The content is the same on both hosts now: the words no longer say whether they can be
        // operated, because the host that takes input lights the line under the pointer instead. This
        // flag decides only whether that host puts a hit box on the line — and, with it, the anchor
        // the d-pad comes to rest on — at all.
        var clickableTeleport = forAddonHost && cfg.ClickTeleportEnabled;
        var content = feed.Compose();
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
