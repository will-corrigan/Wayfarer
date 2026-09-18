using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Ui;

namespace Wayfarer.App.Settings;

/// <summary>The settings window: a game-drawn window with one row per module, a checkbox and a
/// line saying what the module does. It knows the modules only through <see cref="IModuleHost"/>,
/// so a new module appears here by being registered and nothing else.
///
/// <para>The rows are built on every open and torn down on every close, because the game frees
/// the addon's node tree when it closes and rebuilds nothing of ours.</para></summary>
internal sealed class SettingsAddon : NativeAddon
{
    private const float CheckboxHeight = 20f;
    private const float DescriptionHeight = 18f;
    private const float RowGap = 6f;
    private const float RowHeight = CheckboxHeight + DescriptionHeight + RowGap;
    private const float DescriptionIndent = 26f;
    private const uint DescriptionFontSize = 12;
    private const float WindowWidth = 360f;

    /// <summary>The window's title bar, padding and bottom border, which the rows sit inside.</summary>
    private const float WindowChrome = 90f;

    private readonly IModuleHost host;
    private readonly List<NodeBase> rows = [];

    public SettingsAddon(IModuleHost host)
    {
        this.host = host;
        Size = new Vector2(WindowWidth, WindowChrome + (RowHeight * Math.Max(1, host.Modules.Count)));
    }

    /// <inheritdoc/>
    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        var y = ContentStartPosition.Y;
        foreach (var module in host.Modules)
        {
            var toggle = new CheckboxNode
            {
                Position = new Vector2(ContentStartPosition.X, y),
                Size = new Vector2(ContentSize.X, CheckboxHeight),
                String = module.Name,
                IsChecked = host.IsEnabled(module),
            };
            toggle.OnClick = enabled => _ = host.SetEnabledAsync(module, enabled);
            AddNode(toggle);
            rows.Add(toggle);

            var description = new TextNode
            {
                Position = new Vector2(ContentStartPosition.X + DescriptionIndent, y + CheckboxHeight),
                Size = new Vector2(ContentSize.X - DescriptionIndent, DescriptionHeight),
                String = module.Description,
                TextColor = GameColors.Dimmed,
                FontSize = DescriptionFontSize,
            };
            AddNode(description);
            rows.Add(description);

            y += RowHeight;
        }
    }

    /// <inheritdoc/>
    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        foreach (var row in rows)
        {
            row.Dispose();
        }

        rows.Clear();
    }
}
