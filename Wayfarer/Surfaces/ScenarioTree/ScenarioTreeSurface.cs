using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using Wayfarer.App;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Presentation;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Hangs the guidance block inside the game's own Main Scenario Guide and keeps it
/// current. The only class that knows the game's addon exists.
///
/// <para>The block is a child of the addon's root, so the game decides everything about being on
/// screen: when the guide hides, fades, moves in HUD layout or changes scale, the block goes with
/// it. The toolkit's controller builds the block when the addon is set up, and takes it down when
/// the addon is finalized or the plugin unloads.</para>
///
/// <para>Words are re-laid when the guidance changes; the needle and distance are read every frame
/// from <see cref="IHeading"/>.</para></summary>
internal sealed class ScenarioTreeSurface : IAsyncDisposable
{
    private readonly IGuidance guidance;
    private readonly IHeading heading;
    private readonly ITravel travel;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly AddonController controller;
    private GuidanceBlockNode? block;
    private bool wordsChanged = true;
    private bool routeWasPressable;
    private bool broken;

    public unsafe ScenarioTreeSurface(IGuidance guidance, IHeading heading, ITravel travel, ITextureProvider textures, IFramework framework, IPluginLog log)
    {
        this.guidance = guidance;
        this.heading = heading;
        this.travel = travel;
        this.textures = textures;
        this.log = log;

        controller = new AddonController
        {
            AddonName = ScenarioTreeMetrics.AddonName,
            OnSetup = Attach,
            OnFinalize = Detach,
            OnUpdate = Refresh,
        };

        guidance.OnChanged += OnGuidanceChanged;
        _ = framework.RunOnFrameworkThread(controller.Enable);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        guidance.OnChanged -= OnGuidanceChanged;
        await controller.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>How many job-quest rows the game is showing, which is how far down our block goes.</summary>
    private static unsafe int VisibleJobRows(AtkUnitBase* addon)
    {
        var rows = 0;
        foreach (var id in ScenarioTreeMetrics.JobRowNodeIds)
        {
            var node = addon->GetNodeById(id);
            if (node != null && node->IsVisible())
            {
                rows++;
            }
        }

        return rows;
    }

    private void OnGuidanceChanged(object? sender, GuidanceChangedEventArgs e) => wordsChanged = true;

    private unsafe void Attach(AtkUnitBase* addon)
    {
        try
        {
            block = new GuidanceBlockNode(textures, log, OnRoutePressed) { IsVisible = false };
            block.AttachNode(addon);
            wordsChanged = true;
        }
        catch (Exception ex)
        {
            block = null;
            log.Error(ex, "Wayfarer: the guidance block could not be added to the Main Scenario Guide, so nothing is drawn this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        block?.Dispose();
        block = null;
        addon->RootNode->SetHeight((ushort)ScenarioTreeMetrics.RootHeight);
    }

    /// <summary>Grows the addon's root to cover the block while it shows, so clicks on the route
    /// line land inside the addon's own bounds, and shrinks it back when the block hides.</summary>
    private unsafe void FitRootToBlock(AtkUnitBase* addon)
    {
        var wanted = block!.IsVisible
            ? Math.Max(ScenarioTreeMetrics.RootHeight, block.Y + block.Height)
            : ScenarioTreeMetrics.RootHeight;
        if (addon->RootNode->Height != (ushort)wanted)
        {
            addon->RootNode->SetHeight((ushort)wanted);
        }
    }

    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (block is null || broken)
        {
            return;
        }

        try
        {
            block.Position = new Vector2(0f, ScenarioTreeMetrics.JobRowsTop + (ScenarioTreeMetrics.JobRowPitch * VisibleJobRows(addon)));

            if (wordsChanged)
            {
                wordsChanged = false;
                ApplyWords();
                if (block.RoutePressable != routeWasPressable)
                {
                    // The game only dispatches clicks to nodes in the addon's collision list, which
                    // it built at setup, before our box existed or changed.
                    routeWasPressable = block.RoutePressable;
                    addon->UpdateCollisionNodeList(false);
                }
            }

            block.SetHeading(heading.Needle, heading.DistanceYalms);
            FitRootToBlock(addon);
        }
        catch (Exception ex)
        {
            // Once: this runs every frame inside the game's own addon update, and the reason it
            // threw does not change between frames.
            broken = true;
            block.IsVisible = false;
            log.Error(ex, "Wayfarer: drawing the guidance block failed, so it is hidden for this session.");
        }
    }

    private void ApplyWords()
    {
        var current = guidance.Current;
        block!.SetWords(current?.Target?.Text, RouteWords.Compose(current));
    }

    /// <summary>Acts on the route line: read off the guidance at the moment of the press rather
    /// than captured when the line was drawn, so a press can never act on a stale route.</summary>
    private void OnRoutePressed()
    {
        log.Debug("Wayfarer: the route line was pressed.");
        switch (RouteWords.Compose(guidance.Current)?.Press)
        {
            case RoutePress.Teleport teleport:
                travel.TeleportTo(teleport.AetheryteId);
                break;
            case RoutePress.OpenDuty duty:
                travel.OpenDutyFinder(duty.DutyId);
                break;
            default:
                break;
        }
    }
}
