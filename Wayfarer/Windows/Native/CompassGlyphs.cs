using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The compass: a dial that never turns, a needle that does, and the up/down mark that
/// hangs off them when the target is on another level. Three image nodes over three generated
/// textures, owned together because they are drawn together and fail separately.
///
/// <para><b>Why each texture fails on its own.</b> The needle is the mark; without it there is no
/// compass and the caller says so. The ring is its reference marks; without it the needle still
/// points the right way. The chevron is a badge on the ring; without it the distance line still says
/// "above you" in words. Each is generated once per colour, uploaded synchronously, and never
/// retried after a failure — see <see cref="CompassBitmap"/> for the art.</para></summary>
internal sealed class CompassGlyphs(IPluginLog log, ITextureProvider textures, NodeBase parent)
{
    // The ring first so the needle draws over it, concentric with it.
    private readonly Glyph ring = new(
        log,
        textures,
        parent,
        CompassBitmap.Size,
        CompassBitmap.RenderRing,
        "compass ring",
        "Wayfarer guidance: the compass ring could not be generated, so the needle is drawn without its dial.");

    private readonly Glyph needle = new(
        log,
        textures,
        parent,
        CompassBitmap.Size,
        CompassBitmap.RenderNeedle,
        "compass needle",
        "Wayfarer guidance: the compass needle could not be generated, so no compass is drawn this session.");

    private readonly Glyph elevation = new(
        log,
        textures,
        parent,
        ChevronBitmap.Size,
        v => ChevronBitmap.Render(v),
        "elevation mark",
        "Wayfarer guidance: the above/below mark could not be generated. The distance line still says which it is in words.");

    /// <summary>Whether the needle can be drawn in this colour. Asked before layout, because a
    /// compass with no needle falls back to nothing rather than to a bare dial.</summary>
    public bool EnsureNeedle(ArrowIconVariant variant) => needle.Ensure(variant);

    /// <summary>Parks both pieces on one centre, turns the needle to <paramref name="radians"/>, and
    /// hangs the elevation mark off the ring's lower right when the target is above or below.</summary>
    public void Show(Vector2 centre, float ringSize, float needleSize, float radians, ArrowIconVariant variant, ElevationHint hint)
    {
        ring.Park(centre, ringSize);
        ring.Node.IsVisible = ring.Ensure(variant);

        needle.Park(centre, needleSize);
        needle.Node.Rotation = radians;
        needle.Node.IsVisible = true;

        if (hint == ElevationHint.Level || !elevation.Ensure(variant))
        {
            elevation.Node.IsVisible = false;
            return;
        }

        // The art points straight up unrotated, so "above" needs no rotation and "below" is half a
        // turn. Parked clear of the ring's lower right rather than over it: two meanings, two marks.
        var size = Math.Max(needleSize * 0.6f, 8f);
        elevation.Park(ring.Node.Position + new Vector2(ringSize * 0.74f, ringSize * 0.52f) + new Vector2(size / 2f, size / 2f), size);
        elevation.Node.Rotation = hint == ElevationHint.Above ? 0f : MathF.PI;
        elevation.Node.IsVisible = true;
    }

    /// <summary>Takes the whole compass down — every piece, because half a compass is worse than
    /// none.</summary>
    public void Hide()
    {
        ring.Node.IsVisible = false;
        needle.Node.IsVisible = false;
        elevation.Node.IsVisible = false;
    }

    /// <summary>One generated glyph: its node, its renderer, and whether its texture is loaded for
    /// the current colour or has failed for good. <c>FitTexture</c> is exactly right because each
    /// generated texture holds nothing but its own glyph.</summary>
    private sealed class Glyph
    {
        private readonly IPluginLog log;
        private readonly ITextureProvider textures;
        private readonly int size;
        private readonly Func<ArrowIconVariant, byte[]> render;
        private readonly string name;
        private readonly string failure;

        private ArrowIconVariant? loaded;
        private bool failed;

        public Glyph(
            IPluginLog log,
            ITextureProvider textures,
            NodeBase parent,
            int size,
            Func<ArrowIconVariant, byte[]> render,
            string name,
            string failure)
        {
            this.log = log;
            this.textures = textures;
            this.size = size;
            this.render = render;
            this.name = name;
            this.failure = failure;

            Node = new ImGuiImageNode
            {
                TextureSize = new Vector2(size, size),
                Size = new Vector2(GameMetrics.Hud.IconSize, GameMetrics.Hud.IconSize),
                OriginX = GameMetrics.Hud.IconSize / 2f,
                OriginY = GameMetrics.Hud.IconSize / 2f,
                FitTexture = true,
                IsVisible = false,
            };
            Node.AttachNode(parent);
        }

        public ImGuiImageNode Node { get; }

        /// <summary>Sizes the glyph and hangs it off a centre with its rotation origin in the middle
        /// of itself, which is what makes the needle spin in place.</summary>
        public void Park(Vector2 centre, float side)
        {
            Node.Size = new Vector2(side, side);
            Node.OriginX = side / 2f;
            Node.OriginY = side / 2f;
            Node.Position = centre - new Vector2(side / 2f, side / 2f);
        }

        /// <summary>Generates the texture for a colour, once, and hands it to the node — which takes
        /// ownership and disposes it with itself. Reloading on a colour change is what makes the
        /// arrow-colour setting apply live. Never retried after a failure.</summary>
        public bool Ensure(ArrowIconVariant variant)
        {
            if (failed)
            {
                return false;
            }

            if (loaded == variant)
            {
                return true;
            }

            try
            {
                var wrap = textures.CreateFromRaw(
                    RawImageSpecification.Rgba32(size, size), render(variant), $"Wayfarer {name} ({variant})");
                Node.LoadTexture(wrap);
                Node.TextureSize = new Vector2(size, size);
                loaded = variant;
                return true;
            }
            catch (Exception ex)
            {
                failed = true;
                log.Error(ex, failure);
                return false;
            }
        }
    }
}
