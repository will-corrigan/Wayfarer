using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using Wayfarer.App;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Presentation;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Hangs the guidance block inside the game's Main Scenario Guide and keeps it current.
/// The block is a child of the addon's root, so the game decides everything about being on screen.
/// Words are re-laid when the guidance changes; the needle and distance are read every frame.</summary>
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
            AddonName = AddonName,
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

    private static unsafe int VisibleJobRows(AtkUnitBase* addon) =>
        JobRowNodeIds.Count(id => addon->GetNodeById(id) is var node && node != null && node->IsVisible());

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
        addon->RootNode->SetHeight((ushort)RootHeight);
    }

    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (block is null || broken)
        {
            return;
        }

        try
        {
            block.Position = new Vector2(0f, JobRowsTop + (JobRowPitch * VisibleJobRows(addon)));
            RefreshWords(addon);
            block.SetHeading(heading.Needle, heading.DistanceYalms);
            FitRootToBlock(addon);
        }
        catch (Exception ex)
        {
            broken = true;
            block.IsVisible = false;
            log.Error(ex, "Wayfarer: drawing the guidance block failed, so it is hidden for this session.");
        }
    }

    /// <summary>Re-lays the words when the guidance changed. The game only dispatches clicks to
    /// nodes in the addon's collision list, so it is rebuilt when the route line becomes pressable
    /// or stops being.</summary>
    private unsafe void RefreshWords(AtkUnitBase* addon)
    {
        if (!wordsChanged)
        {
            return;
        }

        wordsChanged = false;
        var wasPressable = block!.RoutePressable;
        var current = guidance.Current;
        block.SetWords(current?.Target?.Text, RouteWords.Compose(current));
        if (block.RoutePressable != wasPressable)
        {
            addon->UpdateCollisionNodeList(false);
        }
    }

    /// <summary>The game hit-tests clicks against the root, so it is grown to cover the block
    /// while the block shows and restored when it hides.</summary>
    private unsafe void FitRootToBlock(AtkUnitBase* addon)
    {
        var wanted = (ushort)(block!.IsVisible ? Math.Max(RootHeight, block.Y + block.Height) : RootHeight);
        if (addon->RootNode->Height != wanted)
        {
            addon->RootNode->SetHeight(wanted);
        }
    }

    /// <summary>Read off the guidance at the moment of the press, never captured when the line was
    /// drawn, so a press cannot act on a stale route.</summary>
    private void OnRoutePressed()
    {
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
