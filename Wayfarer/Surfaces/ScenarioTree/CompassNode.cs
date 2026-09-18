using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The compass on screen: a ring that never turns and a needle that does, over two
/// generated textures. The textures are generated the first time the compass is shown; if that
/// fails it is logged once and the compass stays hidden for the session.</summary>
internal sealed class CompassNode : ResNode
{
    private const string RingTextureName = "Wayfarer compass ring";
    private const string NeedleTextureName = "Wayfarer compass needle";

    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly ImGuiImageNode ring = Glyph();
    private readonly ImGuiImageNode needle = Glyph();
    private TextureState state = TextureState.NotLoaded;

    public CompassNode(ITextureProvider textures, IPluginLog log)
    {
        this.textures = textures;
        this.log = log;
        ring.AttachNode(this);
        needle.AttachNode(this);
    }

    private enum TextureState
    {
        NotLoaded,
        Loaded,
        Failed,
    }

    /// <summary>Sizes the compass to a ring box of <paramref name="side"/> and turns the needle.</summary>
    public void Show(float side, float radians)
    {
        IsVisible = LoadTextures();
        if (!IsVisible)
        {
            return;
        }

        Size = new Vector2(side, side);
        Park(ring, side, side);
        Park(needle, side, side * CompassBitmap.NeedleToRingSize);
        needle.Rotation = radians;
    }

    private static ImGuiImageNode Glyph() => new()
    {
        TextureSize = new Vector2(CompassBitmap.Size, CompassBitmap.Size),
        FitTexture = true,
        IsVisible = true,
    };

    /// <summary>Centres a glyph in a square with its rotation origin at its own centre, which is
    /// what makes the needle spin in place.</summary>
    private static void Park(ImGuiImageNode glyph, float within, float side)
    {
        glyph.Size = new Vector2(side, side);
        glyph.OriginX = side / 2f;
        glyph.OriginY = side / 2f;
        glyph.Position = new Vector2((within - side) / 2f, (within - side) / 2f);
    }

    private bool LoadTextures()
    {
        if (state == TextureState.NotLoaded)
        {
            state = TryLoadTextures();
        }

        return state == TextureState.Loaded;
    }

    private TextureState TryLoadTextures()
    {
        try
        {
            ring.LoadTexture(Upload(CompassBitmap.RenderRing(), RingTextureName));
            needle.LoadTexture(Upload(CompassBitmap.RenderNeedle(), NeedleTextureName));
            return TextureState.Loaded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Wayfarer: the compass could not be generated, so none is drawn this session.");
            return TextureState.Failed;
        }
    }

    private IDalamudTextureWrap Upload(byte[] pixels, string name) =>
        textures.CreateFromRaw(RawImageSpecification.Rgba32(CompassBitmap.Size, CompassBitmap.Size), pixels, name);
}
