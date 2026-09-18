using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace Wayfarer.App.Settings;

/// <summary>The settings window: a game-drawn window with one row per module, a checkbox and a
/// line saying what the module does. It knows the modules only through <see cref="IModuleHost"/>,
/// so a new module appears here by being registered and nothing else.
///
/// <para>The rows are built on every open and torn down on every close, because the game frees
/// the addon's node tree when it closes and rebuilds nothing of ours.</para></summary>
internal sealed class SettingsAddon : NativeAddon
{
    private const float RowHeight = 44f;
    private const float DescriptionIndent = 26f;
    private const float WindowWidth = 360f;

    private readonly IModuleHost host;
    private readonly List<NodeBase> rows = [];

    public SettingsAddon(IModuleHost host)
    {
        this.host = host;
        Size = new Vector2(WindowWidth, 90f + (RowHeight * Math.Max(1, host.Modules.Count)));
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
                Size = new Vector2(ContentSize.X, 20f),
                String = module.Name,
                IsChecked = host.IsEnabled(module),
            };
            toggle.OnClick = enabled => _ = host.SetEnabledAsync(module, enabled);
            AddNode(toggle);
            rows.Add(toggle);

            var description = new TextNode
            {
                Position = new Vector2(ContentStartPosition.X + DescriptionIndent, y + 20f),
                Size = new Vector2(ContentSize.X - DescriptionIndent, 18f),
                String = module.Description,
                TextColor = GameColors.Dimmed,
                FontSize = 12,
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
