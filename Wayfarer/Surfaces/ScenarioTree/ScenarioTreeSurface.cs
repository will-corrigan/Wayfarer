using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Wayfarer.App.Settings;
using Wayfarer.Guidance;
using Wayfarer.Presentation;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Hangs the guidance block inside the game's Main Scenario Guide and keeps it current.
/// The block is a child of the addon's root, so the game decides everything about being on screen.
/// Words are re-laid when the guidance changes; the needle and distance are read every frame.
/// Presses are read off the guidance at the moment of the press, never captured when the line was
/// drawn, so a press cannot act on a stale route or entry.
///
/// <para>The pad reaches the block through the guide's own index chain: the plate is a stock button
/// whose layout record chains it to the job rows by index, and our lines are spliced in after
/// the last visible row, before the cursor wraps back to the plate.</para></summary>
internal sealed class ScenarioTreeSurface : IAsyncDisposable
{
    private const string SettingsTooltip = "Wayfarer settings";

    private readonly IGuidance guidance;
    private readonly IHeading heading;
    private readonly GuidancePresses presses;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly ScenarioTreeStyleStore styles;
    private readonly ISettingsWindow settings;
    private readonly AddonController controller;
    private readonly NavSplice splice = new();
    private readonly PlateTakeover takeover = new();
    private readonly PlatePress platePress;
    private GuidanceBlockNode? block;
    private CircleButtonNode? cog;
    private nint plateFocus;
    private ushort? rootHeightBefore;
    private bool wordsChanged = true;
    private bool restyleWanted;
    private bool broken;

    public unsafe ScenarioTreeSurface(IGuidance guidance, IHeading heading, IActions actions, ScenarioTreeStyleStore styles, ISettingsWindow settings, IAddonLifecycle lifecycle, ITextureProvider textures, IFramework framework, IPluginLog log)
    {
        this.guidance = guidance;
        this.heading = heading;
        presses = new GuidancePresses(guidance, actions);
        this.styles = styles;
        this.settings = settings;
        this.textures = textures;
        this.log = log;
        platePress = new PlatePress(lifecycle);
        platePress.Watch(OnPlatePressed);

        controller = new AddonController
        {
            AddonName = AddonName,
            OnSetup = Attach,
            OnFinalize = Detach,
            OnUpdate = Refresh,
        };

        guidance.OnChanged += OnGuidanceChanged;
        styles.OnChanged += OnStyleChanged;
        _ = framework.RunOnFrameworkThread(controller.Enable);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        guidance.OnChanged -= OnGuidanceChanged;
        styles.OnChanged -= OnStyleChanged;
        platePress.Dispose();
        await controller.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>How far down the game's own rows push our block: one pitch per job-quest row with
    /// words in it, and the controller hint strip while the guide has focus.</summary>
    private static unsafe float RowsAbove(AtkUnitBase* addon) =>
        (JobRowPitch * JobRowNodeIds.Count(id => GuideNodes.JobRowShown(addon, id))) + (GuideNodes.HintBarShown(addon) ? HintBarHeight : 0f);

    /// <summary>The node our lines follow in the cursor chain: the last job-quest row showing a
    /// quest, or the plate itself when no row is.</summary>
    private static unsafe uint RowAboveUs(AtkUnitBase* addon)
    {
        foreach (var id in JobRowNodeIds.Reverse())
        {
            if (GuideNodes.JobRowShown(addon, id))
            {
                return id;
            }
        }

        return PlateNodeId;
    }

    private static unsafe void SetRootHeight(AtkUnitBase* addon, ushort height)
    {
        var root = addon->RootNode;
        if (root != null && root->Height != height)
        {
            root->SetHeight(height);
        }
    }

    private void OnGuidanceChanged(object? sender, GuidanceChangedEventArgs e) => wordsChanged = true;

    /// <summary>The settings window may raise this off the framework thread; the block is re-laid
    /// on the next update, which is on it.</summary>
    private void OnStyleChanged(object? sender, EventArgs e) => restyleWanted = true;

    private unsafe void Attach(AtkUnitBase* addon)
    {
        // A setup we have already handled can be delivered again, so anything from last time goes
        // before anything new is made: otherwise the game keeps nodes we no longer hold and can
        // never be told to free.
        Detach(addon);

        try
        {
            var plate = GuideNodes.Plate(addon);
            plateFocus = plate == null || plate->Component == null ? 0 : (nint)plate->Component->GetFocusNode();
            block = new GuidanceBlockNode(textures, log, presses.Entry, presses.Route);
            block.Restyle(styles.Current);
            block.AttachNode(addon);
            cog = new CircleButtonNode
            {
                Icon = CircleButtonIcon.GearCog,
                Position = new Vector2(RootWidth - RightInset - SettingsCogSide, SettingsCogTop),
                Size = new Vector2(SettingsCogSide, SettingsCogSide),
                OnClick = settings.Toggle,
                TextTooltip = SettingsTooltip,
            };
            cog.AttachNode(addon);
            block.GuestOf(addon, (AtkResNode*)plateFocus);
            wordsChanged = true;
        }
        catch (Exception ex)
        {
            cog?.Dispose();
            cog = null;
            block?.Dispose();
            block = null;
            log.Error(ex, "the guidance block could not be added to the Main Scenario Guide, so nothing is drawn this session.");
        }
    }

    /// <summary>Undoes everything the attach did. The lines hand the guide's focus back to the plate
    /// as they dispose, so nothing in the guide can reach a freed node afterwards.</summary>
    private unsafe void Detach(AtkUnitBase* addon)
    {
        splice.Restore(addon);
        takeover.Release(addon);
        cog?.Dispose();
        cog = null;
        block?.Dispose();
        block = null;
        if (rootHeightBefore is { } before)
        {
            SetRootHeight(addon, before);
            rootHeightBefore = null;
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
            block.Position = new Vector2(0f, JobRowsTop + RowsAbove(addon));
            RefreshWords(addon);
            if (restyleWanted)
            {
                restyleWanted = false;
                block.Restyle(styles.Current);
            }

            SpliceIntoChain(addon);

            block.SetHeading(heading.Needle, heading.DistanceYalms, heading.RiseYalms);
            RetitlePlate(addon);
            FitRootToBlock(addon);
        }
        catch (Exception ex)
        {
            broken = true;
            block.IsVisible = false;
            log.Error(ex, "drawing the guidance block failed, so it is hidden for this session.");
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
        block.SetWords(BlockWords.Entry(current?.Target), BlockWords.Route(RouteWords.Compose(current)));
        if (block.AnyPressable != wasPressable)
        {
            addon->UpdateCollisionNodeList(false);
        }
    }

    /// <summary>Our lines go into the cursor chain after the last visible job row, which is what
    /// sits between the plate and us on screen. With no row showing, they follow the plate.</summary>
    private unsafe void SpliceIntoChain(AtkUnitBase* addon)
    {
        if (cog is not null)
        {
            splice.Splice(addon, RowAboveUs(addon), block!, cog);
        }
    }

    /// <summary>The game hit-tests clicks against the root, so it is grown to cover the block while
    /// the block shows. The height it had before we first grew it is remembered, because the guide
    /// sizes its own root past the layout file's default once its rows are showing, and detaching
    /// has to put back what was there rather than what the file says.</summary>
    private unsafe void FitRootToBlock(AtkUnitBase* addon)
    {
        var root = addon == null ? null : addon->RootNode;
        if (root == null)
        {
            return;
        }

        rootHeightBefore ??= root->Height;
        SetRootHeight(addon, (ushort)Math.Max(rootHeightBefore.Value, block!.Y + block.Height));
    }

    /// <summary>While guidance is about something other than the quest the guide names, the plate
    /// carries that name instead; the game's own words come back when it is the guide's own quest
    /// again, which <see cref="PlateTakeover"/> decides by comparing them.</summary>
    private unsafe void RetitlePlate(AtkUnitBase* addon)
    {
        // The plate already names whatever the guide itself is about, so it is retitled only when
        // the guidance is about something else, which is exactly when the source gives it a heading
        // of its own. Whether the plate leads anywhere is a separate question: it names a quest
        // either way, so pressing it opens that quest either way.
        var objective = guidance.Current?.Objective;
        takeover.Update(addon, objective?.Headline, objective?.Kind, objective?.Kind is not null);
    }

    /// <summary>A press of the plate opens the page about whatever the plate names, whether those
    /// are our words or the guide's own. The game's own handling is not reached, because the press
    /// is answered here; a plate that leads nowhere is left entirely alone.</summary>
    private bool OnPlatePressed()
    {
        if (guidance.Current is not { Objective.HeadlinePressable: true })
        {
            return false;
        }

        presses.Headline();
        return true;
    }
}
