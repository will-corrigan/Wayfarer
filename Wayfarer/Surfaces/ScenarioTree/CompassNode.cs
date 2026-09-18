using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The compass on screen: a ring that never turns and a needle that does, two image
/// nodes over two generated textures. Sized by its ring; the needle is scaled to it so the two
/// stay concentric and in proportion.
///
/// <para>The textures are generated and uploaded the first time the compass is shown. If that
/// fails it is logged once and the compass stays hidden for the session; the words still show.</para></summary>
internal sealed class CompassNode : ResNode
{
    private const string RingTextureName = "Wayfarer compass ring";
    private const string NeedleTextureName = "Wayfarer compass needle";

    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly ImGuiImageNode ring;
    private readonly ImGuiImageNode needle;
    private bool loaded;
    private bool failed;

    public CompassNode(ITextureProvider textures, IPluginLog log)
    {
        this.textures = textures;
        this.log = log;

        // The ring first so the needle draws over it.
        ring = Glyph();
        ring.AttachNode(this);
        needle = Glyph();
        needle.AttachNode(this);
    }

    /// <summary>Sizes the compass to a ring box of <paramref name="side"/>, centred on this node's
    /// origin, and turns the needle to <paramref name="radians"/>.</summary>
    public void Show(float side, float radians)
    {
        if (!EnsureLoaded())
        {
            IsVisible = false;
            return;
        }

        Size = new Vector2(side, side);
        Park(ring, side, side);
        Park(needle, side, side * CompassBitmap.NeedleToRingSize);
        needle.Rotation = radians;
        IsVisible = true;
    }

    private static ImGuiImageNode Glyph() => new()
    {
        TextureSize = new Vector2(CompassBitmap.Size, CompassBitmap.Size),
        FitTexture = true,
        IsVisible = true,
    };

    /// <summary>Puts a glyph's box in the middle of a <paramref name="within"/>-wide square with
    /// its rotation origin at its own centre, which is what makes the needle spin in place.</summary>
    private static void Park(ImGuiImageNode glyph, float within, float side)
    {
        glyph.Size = new Vector2(side, side);
        glyph.OriginX = side / 2f;
        glyph.OriginY = side / 2f;
        glyph.Position = new Vector2((within - side) / 2f, (within - side) / 2f);
    }

    private bool EnsureLoaded()
    {
        if (loaded)
        {
            return true;
        }

        if (failed)
        {
            return false;
        }

        try
        {
            ring.LoadTexture(Upload(CompassBitmap.RenderRing(), RingTextureName));
            needle.LoadTexture(Upload(CompassBitmap.RenderNeedle(), NeedleTextureName));
            loaded = true;
            return true;
        }
        catch (Exception ex)
        {
            failed = true;
            log.Error(ex, "Wayfarer: the compass could not be generated, so none is drawn this session.");
            return false;
        }
    }

    private Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap Upload(byte[] pixels, string name) =>
        textures.CreateFromRaw(RawImageSpecification.Rgba32(CompassBitmap.Size, CompassBitmap.Size), pixels, name);
}
