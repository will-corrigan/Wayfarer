using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;
using Wayfarer.Core.Ui;

namespace Wayfarer.Ui;

/// <summary>Wayfarer's own mark, the compass ring, as something the player can press: the plugin
/// putting its name to a control it has added to one of the game's windows. It sits dim until the
/// pointer is over it or it is holding a state on, which is the game's own habit for a mark that
/// is also a switch.</summary>
internal sealed class WayfarerMark : ResNode
{
    private const string TextureName = "Wayfarer mark";
    private const float DimAlpha = 0.55f;

    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly ImGuiImageNode mark;
    private readonly CollisionNode press = new();
    private bool loaded;
    private bool failed;

    public WayfarerMark(ITextureProvider textures, IPluginLog log, Action onPressed)
    {
        this.textures = textures;
        this.log = log;

        mark = new ImGuiImageNode
        {
            TextureSize = new Vector2(GlyphCanvas.Size, GlyphCanvas.Size),
            FitTexture = true,
            Alpha = DimAlpha,
        };
        mark.AttachNode(this);

        press.ShowClickableCursor = true;
        press.AddEvent(AtkEventType.MouseClick, onPressed);
        press.AddEvent(AtkEventType.MouseOver, () => mark.Alpha = 1f);
        press.AddEvent(AtkEventType.MouseOut, Settle);
        press.AttachNode(this);
    }

    /// <summary>What the pointer is told the mark does.</summary>
    public ReadOnlySeString Tooltip
    {
        get => press.TextTooltip;
        set => press.TextTooltip = value;
    }

    /// <summary>Whether the mark stands for something that is on: lit rather than dim.</summary>
    public bool Lit
    {
        get;
        set
        {
            field = value;
            Settle();
        }
    }

    /// <summary>Shows the mark at a size, generating its texture the first time. A failure is
    /// logged once and the mark stays hidden for the session.</summary>
    public void Show(float side)
    {
        IsVisible = Load();
        if (!IsVisible)
        {
            return;
        }

        Size = new Vector2(side, side);
        mark.Size = Size;
        press.Size = Size;
    }

    private void Settle() => mark.Alpha = Lit ? 1f : DimAlpha;

    private bool Load()
    {
        if (loaded || failed)
        {
            return loaded;
        }

        try
        {
            mark.LoadTexture(Upload(CompassBitmap.RenderRing()));
            loaded = true;
        }
        catch (Exception ex)
        {
            failed = true;
            log.Error(ex, "Wayfarer's own mark could not be generated, so the controls that carry it are not shown this session.");
        }

        return loaded;
    }

    private IDalamudTextureWrap Upload(byte[] pixels) =>
        textures.CreateFromRaw(RawImageSpecification.Rgba32(GlyphCanvas.Size, GlyphCanvas.Size), pixels, TextureName);
}
