using System.Numerics;
using Dalamud.Plugin.Services;
using KamiToolKit.Enums;
using KamiToolKit.UiOverlay;

namespace Wayfarer.Windows.Native;

/// <summary>The click-through host for the guidance readout — a thin shell around
/// <see cref="ReadoutBodyNode"/>, which is where everything the readout looks like actually lives.
///
/// It sits on the overlay layer above nameplates and below the player's own windows, where the
/// toolkit has already made it click-through, unfocusable and outside controller navigation, and
/// where it hides itself during cutscenes and with the Toggle UI Display hotkey. That is what makes
/// it the right host for a controller: it cannot be in the way, cannot steal focus and cannot trap
/// the cursor. It is also why it cannot carry the teleport click — see
/// <see cref="ClickableReadoutAddon"/>, which hosts the identical body for a mouse.
///
/// <b>It must never throw.</b> <c>OnUpdate</c> runs every frame from the addon's update hook, so an
/// exception here is an exception sixty times a second inside the game's render path. The whole
/// body is wrapped; the first failure hides the node permanently and logs once.</summary>
internal sealed class GuidanceOverlayNode : OverlayNode
{
    private readonly Func<ReadoutFrame?> provider;
    private readonly IPluginLog log;
    private readonly ReadoutBodyNode body;

    private bool broken;

    public GuidanceOverlayNode(Func<ReadoutFrame?> provider, IPluginLog log, Func<bool> diagnosticsEnabled)
    {
        this.provider = provider;
        this.log = log;

        // No click handler: an overlay is click-through by construction, so offering one would be
        // a lie. The body renders identically either way.
        body = new ReadoutBodyNode(log, diagnosticsEnabled);
        body.AttachNode(this);
    }

    /// <inheritdoc/>
    public override OverlayLayer OverlayLayer => OverlayLayer.BehindUserInterface;

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        if (broken)
        {
            return;
        }

        try
        {
            Render();
        }
        catch (Exception ex)
        {
            broken = true;
            IsVisible = false;
            log.Error(ex, "Wayfarer readout: the overlay failed and has switched itself off for this session.");
        }
    }

    private void Render()
    {
        if (provider() is not { } frame || frame.Content.IsEmpty)
        {
            body.HideAll();
            IsVisible = false;
            return;
        }

        IsVisible = true;
        var size = body.Layout(frame);
        body.Position = Vector2.Zero;
        Size = size;
        Position = ReadoutPlacement.Resolve(frame.Position, size);
    }
}
