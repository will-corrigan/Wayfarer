using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using KamiToolKit.Nodes;
using Wayfarer.App;

using Wayfarer.Guidance;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The compass on screen: a ring that never turns, a needle that does, and the
/// above/below mark hung off the ring's lower right when the target is on another level. Three
/// image nodes over three generated textures, generated the first time the compass is shown; if
/// that fails it is logged once and the compass stays hidden for the session.</summary>
internal sealed class CompassNode : ResNode
{
    private const string RingTextureName = "Wayfarer compass ring";
    private const string NeedleTextureName = "Wayfarer compass needle";
    private const string ChevronTextureName = "Wayfarer elevation mark";

    /// <summary>The mark's size and place, as fractions of the ring's box: a little over half
    /// the ring, parked clear of its lower right.</summary>
    private const float MarkScale = 0.55f;
    private const float MarkOffsetX = 0.74f;
    private const float MarkOffsetY = 0.52f;

    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly ImGuiImageNode ring = Glyph(GlyphCanvas.Size);
    private readonly ImGuiImageNode needle = Glyph(GlyphCanvas.Size);
    private readonly ImGuiImageNode mark = Glyph(GlyphCanvas.Size);
    private TextureState state = TextureState.NotLoaded;

    public CompassNode(ITextureProvider textures, IPluginLog log)
    {
        this.textures = textures;
        this.log = log;
        ring.AttachNode(this);
        needle.AttachNode(this);
        mark.AttachNode(this);
    }

    private enum TextureState
    {
        NotLoaded,
        Loaded,
        Failed,
    }

    /// <summary>Sizes the compass to a ring box of <paramref name="side"/>, turns the needle, and
    /// shows the above/below mark when the target is on another level.</summary>
    public void Show(float side, float radians, ElevationHint elevation)
    {
        IsVisible = LoadTextures();
        if (!IsVisible)
        {
            return;
        }

        Size = new Vector2(side, side);
        Park(ring, side, side, Vector2.Zero);
        Park(needle, side, side * CompassBitmap.NeedleToRingSize, Vector2.Zero);
        needle.Rotation = radians;

        mark.IsVisible = elevation != ElevationHint.Level;
        Park(mark, side, side * MarkScale, new Vector2(side * MarkOffsetX, side * MarkOffsetY));
        mark.Rotation = elevation == ElevationHint.Below ? MathF.PI : 0f;
    }

    private static ImGuiImageNode Glyph(int textureSide) => new()
    {
        TextureSize = new Vector2(textureSide, textureSide),
        FitTexture = true,
        IsVisible = true,
    };

    /// <summary>Centres a glyph in a square, shifted by <paramref name="offset"/>, with its rotation
    /// origin at its own centre so it turns in place.</summary>
    private static void Park(ImGuiImageNode glyph, float within, float side, Vector2 offset)
    {
        glyph.Size = new Vector2(side, side);
        glyph.OriginX = side / 2f;
        glyph.OriginY = side / 2f;
        glyph.Position = new Vector2((within - side) / 2f, (within - side) / 2f) + offset;
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
            ring.LoadTexture(Upload(CompassBitmap.RenderRing(), GlyphCanvas.Size, RingTextureName));
            needle.LoadTexture(Upload(CompassBitmap.RenderNeedle(), GlyphCanvas.Size, NeedleTextureName));
            mark.LoadTexture(Upload(ChevronBitmap.Render(), GlyphCanvas.Size, ChevronTextureName));
            return TextureState.Loaded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "the compass could not be generated, so none is drawn this session.");
            return TextureState.Failed;
        }
    }

    private IDalamudTextureWrap Upload(byte[] pixels, int side, string name) =>
        textures.CreateFromRaw(RawImageSpecification.Rgba32(side, side), pixels, name);
}
