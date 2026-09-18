using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using Lumina.Text.ReadOnly;
using Wayfarer.App;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Presentation;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Hangs the guidance block inside the game's Main Scenario Guide and keeps it current.
/// The block is a child of the addon's root, so the game decides everything about being on screen.
/// Words are re-laid when the guidance changes; the needle and distance are read every frame.
/// Presses are read off the guidance at the moment of the press, never captured when the line was
/// drawn, so a press cannot act on a stale route or entry.
///
/// <para>The pad reaches the block through the plate: HUD Select lands on the plate as it always
/// has, a Down press there is heard by our own listener on the plate's node, and the cursor is
/// moved to our first line. From there the lines move it between themselves and back up to the
/// plate.</para></summary>
internal sealed class ScenarioTreeSurface : IAsyncDisposable, IDiagnostics
{
    private const string SayCommand = "/say ";

    private readonly IGuidance guidance;
    private readonly IHeading heading;
    private readonly IActions actions;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly IFramework framework;
    private readonly AddonController controller;
    private readonly PlatePadListener plateListener;
    private GuidanceBlockNode? block;
    private nint addonAddress;
    private nint plateFocus;
    private bool wordsChanged = true;
    private bool broken;

    public unsafe ScenarioTreeSurface(IGuidance guidance, IHeading heading, IActions actions, ITextureProvider textures, IFramework framework, IPluginLog log)
    {
        this.guidance = guidance;
        this.heading = heading;
        this.actions = actions;
        this.textures = textures;
        this.log = log;
        this.framework = framework;

        plateListener = new PlatePadListener(OnPlateDown);
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
        await framework.RunOnFrameworkThread(plateListener.Dispose).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public unsafe void Dump()
    {
        var addon = (AtkUnitBase*)addonAddress;
        if (addon == null || block is null)
        {
            log.Information("Wayfarer nav: the Main Scenario Guide is not open, or the block is not attached.");
            return;
        }

        var plate = Plate(addon);
        var input = AtkStage.Instance()->AtkInputManager;
        log.Information($"Wayfarer nav: addon focus {(nint)addon->FocusNode:X} component focus {(nint)addon->ComponentFocusNode:X} own index {addon->CursorNavigationOwnIndex} flags1A2 {addon->Flags1A2:X2} root {addon->RootNode->Width}x{addon->RootNode->Height}");
        log.Information($"Wayfarer nav: input focused node {(nint)input->FocusedNode:X} focus list index {input->FocusListIndex}");
        if (plate != null && plate->Component != null)
        {
            var nav = plate->Component->CursorNavigationInfo;
            log.Information($"Wayfarer nav: plate node {(nint)plate:X} focus node {(nint)plate->Component->GetFocusNode():X} index {nav.Index} up {nav.UpIndex} down {nav.DownIndex} left {nav.LeftIndex} right {nav.RightIndex} mode {nav.NavigationMode} cursor type {nav.CursorType}");
        }

        log.Information($"Wayfarer nav: our lines {string.Join(", ", block.FocusTargets.Select(t => t.ToString("X", System.Globalization.CultureInfo.InvariantCulture)))} pressable {block.AnyPressable}");
        for (var i = 0; i < addon->CollisionNodeListCount; i++)
        {
            var node = addon->CollisionNodeList[i];
            log.Information($"Wayfarer nav: collision {i} node {(nint)node:X} id {node->NodeId} flags {node->NodeFlags} at {node->X},{node->Y} {node->Width}x{node->Height}");
        }
    }

    private static unsafe bool NodeShown(AtkUnitBase* addon, uint id) =>
        addon->GetNodeById(id) is var node && node != null && node->IsVisible();

    /// <summary>How far down the game's own rows push our block: one pitch per job-quest row, and
    /// the controller hint strip while the guide has focus.</summary>
    private static unsafe float RowsAbove(AtkUnitBase* addon) =>
        (JobRowPitch * JobRowNodeIds.Count(id => NodeShown(addon, id))) + (NodeShown(addon, HintBarNodeId) ? HintBarHeight : 0f);

    private static unsafe AtkComponentNode* Plate(AtkUnitBase* addon)
    {
        var node = addon->GetNodeById(PlateNodeId);
        return node == null || (int)node->Type < (int)NodeType.Component ? null : (AtkComponentNode*)node;
    }

    private static LineContent? EntryContent(ObjectiveEntry? entry) =>
        entry is null ? null : new LineContent(EntryWords.Describe(entry), IconFor(entry.Action), entry.Action is not null);

    private static uint? IconFor(EntryAction? action) => action switch
    {
        EntryAction.UseItem item => item.IconId,
        EntryAction.Emote emote => emote.IconId,
        _ => null,
    };

    private static LineContent? RouteContent(RouteLine? line) =>
        line is null ? null : new LineContent(WithGlyph(line), null, line.Press is not null);

    private static ReadOnlySeString WithGlyph(RouteLine line) =>
        Glyph(line.Glyph) is { } icon
            ? new ReadOnlySeString(new SeStringBuilder().AddIcon(icon).AddText(line.Text).Build().Encode())
            : line.Text;

    private static BitmapFontIcon? Glyph(RouteGlyph glyph) => glyph switch
    {
        RouteGlyph.Aetheryte => BitmapFontIcon.Aetheryte,
        RouteGlyph.Duty => BitmapFontIcon.WaitingForDutyFinder,
        _ => null,
    };

    private void OnGuidanceChanged(object? sender, GuidanceChangedEventArgs e) => wordsChanged = true;

    private unsafe void Attach(AtkUnitBase* addon)
    {
        try
        {
            addonAddress = (nint)addon;
            plateFocus = (nint)addon->FocusNode;
            block = new GuidanceBlockNode(textures, log, OnEntryPressed, OnRoutePressed) { IsVisible = false };
            block.AttachNode(addon);
            wordsChanged = true;

            var plate = Plate(addon);
            if (plate != null && plate->Component != null)
            {
                plateListener.Attach(plate);
            }
        }
        catch (Exception ex)
        {
            block = null;
            log.Error(ex, "Wayfarer: the guidance block could not be added to the Main Scenario Guide, so nothing is drawn this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        plateListener.Detach();
        block?.Dispose();
        block = null;
        addonAddress = 0;
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
            block.Position = new Vector2(0f, JobRowsTop + RowsAbove(addon));
            RefreshWords(addon);
            block.WireFocus(addon, (AtkResNode*)plateFocus);
            block.SetHeading(heading.Needle, heading.DistanceYalms, heading.RiseYalms);
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
    /// nodes in the addon's collision list, so it is rebuilt when a line becomes pressable or
    /// stops being.</summary>
    private unsafe void RefreshWords(AtkUnitBase* addon)
    {
        if (!wordsChanged)
        {
            return;
        }

        wordsChanged = false;
        var wasPressable = block!.AnyPressable;
        var current = guidance.Current;
        block.SetWords(EntryContent(current?.Target), RouteContent(RouteWords.Compose(current)));
        if (block.AnyPressable != wasPressable)
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

    /// <summary>The pad pressed Down while the plate had the cursor: move it to our first line.</summary>
    private unsafe void OnPlateDown()
    {
        var targets = block?.FocusTargets;
        if (targets is { Length: > 0 } && addonAddress != 0)
        {
            AtkStage.Instance()->AtkInputManager->SetFocus((AtkResNode*)targets[0], (AtkUnitBase*)addonAddress, 0);
        }
    }

    private void OnEntryPressed()
    {
        switch (guidance.Current?.Target?.Action)
        {
            case EntryAction.UseItem item:
                actions.UseItem(item.ItemId);
                break;
            case EntryAction.Emote emote:
                actions.Emote(emote.EmoteId);
                break;
            case EntryAction.Say say:
                actions.FillChat(SayCommand + say.Phrase);
                break;
            default:
                break;
        }
    }

    private void OnRoutePressed()
    {
        switch (RouteWords.Compose(guidance.Current)?.Press)
        {
            case RoutePress.Teleport teleport:
                actions.TeleportTo(teleport.AetheryteId);
                break;
            case RoutePress.OpenDuty duty:
                actions.OpenDutyFinder(duty.DutyId);
                break;
            default:
                break;
        }
    }
}
