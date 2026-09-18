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
/// drawn, so a press cannot act on a stale route or entry.</summary>
internal sealed class ScenarioTreeSurface : IAsyncDisposable
{
    private const string SayCommand = "/say ";

    private readonly IGuidance guidance;
    private readonly IHeading heading;
    private readonly IActions actions;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly AddonController controller;
    private GuidanceBlockNode? block;
    private bool wordsChanged = true;
    private bool broken;
    private byte? plateDownBeforeUs;

    public unsafe ScenarioTreeSurface(IGuidance guidance, IHeading heading, IActions actions, ITextureProvider textures, IFramework framework, IPluginLog log)
    {
        this.guidance = guidance;
        this.heading = heading;
        this.actions = actions;
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
            block = new GuidanceBlockNode(textures, log, OnEntryPressed, OnRoutePressed) { IsVisible = false };
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
        if (plateDownBeforeUs is { } original && Plate(addon) is var plate && plate != null)
        {
            plate->Component->CursorNavigationInfo.DownIndex = original;
        }

        plateDownBeforeUs = null;
        block?.Dispose();
        block = null;
        addon->RootNode->SetHeight((ushort)RootHeight);
    }

    /// <summary>Hangs our stops under the game's plate in the controller's navigation: pressing
    /// down on the plate reaches our first pressable line, and up from it returns. The plate's
    /// original down is kept and restored when we detach. Re-applied every frame, because the
    /// game rebuilds its own navigation when it refreshes.</summary>
    private unsafe void LinkControllerNav(AtkUnitBase* addon)
    {
        var plate = Plate(addon);
        if (plate == null || plate->Component == null)
        {
            return;
        }

        ref var nav = ref plate->Component->CursorNavigationInfo;
        plateDownBeforeUs ??= nav.DownIndex;
        nav.DownIndex = (byte)(block!.FirstStop ?? plateDownBeforeUs.Value);
        block.LinkNav(nav.Index);
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
            LinkControllerNav(addon);
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
