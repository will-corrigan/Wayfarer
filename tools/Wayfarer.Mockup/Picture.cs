using Lumina;
using Lumina.Data.Files;

namespace Wayfarer.Mockup;

/// <summary>A piece of the game's art, read straight out of sqpack and turned the right way round.
/// The game stores its pictures blue first; reading them as though red came first is how a gold
/// star comes out blue.</summary>
internal sealed class Picture(int width, int height, byte[] pixels)
{
    public int Width => width;

    public int Height => height;

    public byte[] Pixels => pixels;

    public static Picture From(GameData game, string path)
    {
        var tex = game.GetFile<TexFile>(path) ?? throw new FileNotFoundException($"no such picture: {path}");
        return new Picture(tex.Header.Width, tex.Header.Height, Straighten(tex.ImageData));
    }

    /// <summary>The raw pixels of a picture, left blue-first: the font textures hold coverage in
    /// each channel rather than a colour, so straightening them would only shuffle the channels a
    /// glyph is looked up in.</summary>
    public static Picture Raw(GameData game, string path)
    {
        var tex = game.GetFile<TexFile>(path) ?? throw new FileNotFoundException($"no such picture: {path}");
        return new Picture(tex.Header.Width, tex.Header.Height, tex.ImageData);
    }

    private static byte[] Straighten(ReadOnlySpan<byte> pixels)
    {
        var rgba = new byte[pixels.Length];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (pixels[i + 2], pixels[i + 1], pixels[i], pixels[i + 3]);
        }

        return rgba;
    }
}
