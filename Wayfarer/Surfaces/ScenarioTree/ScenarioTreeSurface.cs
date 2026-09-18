using System.Globalization;
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
/// <para>The pad does not reach the block yet. The guide's plate is a stock button whose layout
/// record chains it to the two job rows by index (2, 3, 4). Linking our lines into that chain is
/// an investigation switch, off by default, toggled by <c>/wayfarer pad</c>; the cursor's every
/// move is logged so a remote tester can be read.</para></summary>
internal sealed class ScenarioTreeSurface : IAsyncDisposable, IDiagnostics
{
    private const string SayCommand = "/say ";
    private const string PadSwitch = "pad";

    private readonly IGuidance guidance;
    private readonly IHeading heading;
    private readonly IActions actions;
    private readonly ITextureProvider textures;
    private readonly ILog log;
    private readonly AddonController controller;
    private GuidanceBlockNode? block;
    private nint addonAddress;
    private nint plateFocus;
    private nint lastFocused;
    private byte? plateDownBeforeUs;
    private bool padLinking;
    private bool wordsChanged = true;
    private bool broken;

    public unsafe ScenarioTreeSurface(IGuidance guidance, IHeading heading, IActions actions, ITextureProvider textures, IFramework framework, ILog log)
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

    /// <inheritdoc/>
    public unsafe void Dump()
    {
        var addon = (AtkUnitBase*)addonAddress;
        if (addon == null || block is null)
        {
            log.Info("nav: the Main Scenario Guide is not open, or the block is not attached.");
            return;
        }

        var input = AtkStage.Instance()->AtkInputManager;
        log.Info($"nav: pad linking {padLinking}; addon focus {Hex((nint)addon->FocusNode)} component focus {Hex((nint)addon->ComponentFocusNode)} own index {addon->CursorNavigationOwnIndex} flags1A2 {addon->Flags1A2:X2} root {addon->RootNode->Width}x{addon->RootNode->Height}");
        log.Info($"nav: input focused node {Hex((nint)input->FocusedNode)} focus list index {input->FocusListIndex}");
        var plate = Plate(addon);
        if (plate != null && plate->Component != null)
        {
            var nav = plate->Component->CursorNavigationInfo;
            log.Info($"nav: plate node {Hex((nint)plate)} focus node {Hex((nint)plate->Component->GetFocusNode())} index {nav.Index} up {nav.UpIndex} down {nav.DownIndex} left {nav.LeftIndex} right {nav.RightIndex} mode {nav.NavigationMode} cursor type {nav.CursorType}");
        }

        log.Info($"nav: our lines {string.Join(", ", block.FocusTargets.Select(Hex))} pressable {block.AnyPressable} first stop {block.FirstStop}");
        for (var i = 0; i < addon->CollisionNodeListCount; i++)
        {
            var node = addon->CollisionNodeList[i];
            var component = node->Type == NodeType.Collision ? ((AtkCollisionNode*)node)->LinkedComponent : null;
            var index = component == null ? "-" : component->CursorNavigationInfo.Index.ToString(CultureInfo.InvariantCulture);
            log.Info($"nav: collision {i} node {Hex((nint)node)} id {node->NodeId} flags {node->NodeFlags} nav index {index} at {node->X},{node->Y} {node->Width}x{node->Height}");
        }
    }

    /// <inheritdoc/>
    public string? Toggle(string switchName)
    {
        if (!string.Equals(switchName, PadSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        padLinking = !padLinking;
        log.Info($"nav: pad linking is now {(padLinking ? "on" : "off")}");
        return padLinking ? "on" : "off";
    }

    private static string Hex(nint address) => address.ToString("X", CultureInfo.InvariantCulture);

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
            log.Debug($"nav: block attached to the guide; addon {Hex(addonAddress)} plate focus {Hex(plateFocus)}");
        }
        catch (Exception ex)
        {
            block = null;
            log.Error("the guidance block could not be added to the Main Scenario Guide, so nothing is drawn this session.", ex);
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        RestorePlateLink(addon);
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
            if (padLinking)
            {
                LinkIntoPlateChain(addon);
            }
            else
            {
                RestorePlateLink(addon);
            }

            block.WireFocus(addon, (AtkResNode*)plateFocus);
            block.SetHeading(heading.Needle, heading.DistanceYalms, heading.RiseYalms);
            FitRootToBlock(addon);
            TraceFocus(addon);
        }
        catch (Exception ex)
        {
            broken = true;
            block.IsVisible = false;
            log.Error("drawing the guidance block failed, so it is hidden for this session.", ex);
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

    /// <summary>The investigation: point the plate's "down" at our first line and our lines' records
    /// back at the plate, so the game's own index navigation can find us if it looks.</summary>
    private unsafe void LinkIntoPlateChain(AtkUnitBase* addon)
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

    private unsafe void RestorePlateLink(AtkUnitBase* addon)
    {
        if (plateDownBeforeUs is { } original && Plate(addon) is var plate && plate != null && plate->Component != null)
        {
            plate->Component->CursorNavigationInfo.DownIndex = original;
        }

        plateDownBeforeUs = null;
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

    /// <summary>Logs every move of the game's input focus, saying whether it landed on the plate,
    /// on one of our lines, or elsewhere.</summary>
    private unsafe void TraceFocus(AtkUnitBase* addon)
    {
        var focused = (nint)AtkStage.Instance()->AtkInputManager->FocusedNode;
        if (focused == lastFocused)
        {
            return;
        }

        lastFocused = focused;
        var where = focused == 0 ? "nothing"
            : focused == plateFocus ? "the plate"
            : block!.FocusTargets.Contains(focused) ? "one of our lines"
            : "elsewhere";
        log.Debug($"nav: input focus moved to {Hex(focused)} ({where}); addon focus node {Hex((nint)addon->FocusNode)}");
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
