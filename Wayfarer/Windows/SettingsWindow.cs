using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;
using Wayfarer.Windows.Native;

namespace Wayfarer.Windows;

/// <summary>Wayfarer's settings window, which in this build says only that settings are still to
/// come. The old settings tab was retired with the hub it lived in; what replaces it is built from
/// the ground up, and this is the ground: one native window, one line of text, so every door that
/// used to open settings still opens something honest.</summary>
internal sealed unsafe class SettingsWindow : NativeAddon
{
    private const float WindowWidth = 360f;
    private const float WindowHeight = 140f;
    private const string Placeholder = "To be implemented.";

    private readonly IFramework framework;
    private readonly IPluginLog log;
    private TextNode? note;

    public SettingsWindow(IFramework framework, IPluginLog log)
    {
        this.framework = framework;
        this.log = log;
        ContentPadding = new Vector2(GameMetrics.Window.InsetLeft, GameMetrics.Window.BlockGap);
    }

    /// <summary>Opens the window, sized in addon units — the game renders it at the player's
    /// interface scale, so screen pixels are divided by that scale exactly once here.</summary>
    public void OpenWindow()
    {
        if (IsOpen)
        {
            return;
        }

        Size = new Vector2(WindowWidth, WindowHeight) / Math.Max(AtkUnitBase.GetGlobalUIScale(), 0.1f);
        Open();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Dalamud unloads plugins on a thread-pool thread; base.Dispose() asserts the main thread.
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
            log.Warning(ex, "Wayfarer settings: disposing the window on the framework thread failed or timed out, so its nodes are leaked until the game is restarted.");
        }
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        note = new TextNode
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.BodySize,
            LineSpacing = GameMetrics.Type.BodyLine,
            AlignmentType = AlignmentType.Center,
            TextColor = GameColors.Body,
            TextOutlineColor = GameColors.BodyEdge,
            TextFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine,
            String = Placeholder,
        };
        note.AttachNode(this);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        note?.Dispose();
        note = null;
    }
}
