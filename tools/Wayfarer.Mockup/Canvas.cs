using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Numerics;
using System.Text;

namespace Wayfarer.Mockup;

/// <summary>A picture being drawn: straight RGBA, one byte a channel, red first. Everything drawn
/// on it is blended over what is already there, the way the game blends its own layers, so a piece
/// of art with a torn or soft edge lands on the page looking as it does in the game.</summary>
internal sealed class Canvas(int width, int height)
{
    private readonly byte[] pixels = new byte[width * height * 4];
    private Vector4? clip;

    public int Width => width;

    public int Height => height;

    /// <summary>Keeps everything drawn after this inside a box, the way a scrolling pane keeps its
    /// page inside itself. Without it a mockup shows words the window would never show.</summary>
    public IDisposable Clip(float x, float y, float w, float h)
    {
        var was = clip;
        clip = new Vector4(x, y, x + w, y + h);
        return new Restore(() => clip = was);
    }

    /// <summary>Fills the whole picture with one colour, replacing rather than blending, so the
    /// mockup starts from a known ground rather than from black.</summary>
    public void Clear(Vector4 colour)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = Channel(colour.X);
            pixels[i + 1] = Channel(colour.Y);
            pixels[i + 2] = Channel(colour.Z);
            pixels[i + 3] = Channel(colour.W);
        }
    }

    /// <summary>Blends a colour over a rectangle.</summary>
    public void Fill(float x, float y, float w, float h, Vector4 colour)
    {
        var left = (int)MathF.Round(x);
        var top = (int)MathF.Round(y);
        var right = (int)MathF.Round(x + w);
        var bottom = (int)MathF.Round(y + h);

        for (var py = top; py < bottom; py++)
        {
            for (var px = left; px < right; px++)
            {
                Blend(px, py, colour.X, colour.Y, colour.Z, colour.W);
            }
        }
    }

    /// <summary>Draws the outline of a rectangle, a line thick, for showing where a thing's own
    /// box falls rather than for anything the game itself draws.</summary>
    public void Outline(float x, float y, float w, float h, Vector4 colour, float thickness = 1f)
    {
        Fill(x, y, w, thickness, colour);
        Fill(x, y + h - thickness, w, thickness, colour);
        Fill(x, y, thickness, h, colour);
        Fill(x + w - thickness, y, thickness, h, colour);
    }

    /// <summary>Draws a picture into a box, stretched to fill it, sampling nearest — the same
    /// stretch the game does when a one-piece background is given a size that is not its own.</summary>
    public void Stretch(Picture art, float x, float y, float w, float h, Vector4? tint = null)
    {
        var shade = tint ?? Vector4.One;
        var left = (int)MathF.Round(x);
        var top = (int)MathF.Round(y);
        var across = (int)MathF.Round(w);
        var down = (int)MathF.Round(h);
        if (across <= 0 || down <= 0)
        {
            return;
        }

        for (var py = 0; py < down; py++)
        {
            var sy = Math.Clamp((int)((py + 0.5f) / down * art.Height), 0, art.Height - 1);
            for (var px = 0; px < across; px++)
            {
                var sx = Math.Clamp((int)((px + 0.5f) / across * art.Width), 0, art.Width - 1);
                var at = ((sy * art.Width) + sx) * 4;
                Blend(
                    left + px,
                    top + py,
                    art.Pixels[at] / 255f * shade.X,
                    art.Pixels[at + 1] / 255f * shade.Y,
                    art.Pixels[at + 2] / 255f * shade.Z,
                    art.Pixels[at + 3] / 255f * shade.W);
            }
        }
    }

    /// <summary>Draws a picture at its own size.</summary>
    public void Draw(Picture art, float x, float y, Vector4? tint = null) => Stretch(art, x, y, art.Width, art.Height, tint);

    /// <summary>Puts one coverage value on the picture in a colour: how a glyph is drawn, where the
    /// font's texture holds how much of each pixel the letter covers and nothing else.</summary>
    public void Ink(int x, int y, float coverage, Vector4 colour)
    {
        if (coverage > 0f)
        {
            Blend(x, y, colour.X, colour.Y, colour.Z, colour.W * coverage);
        }
    }

    public void Save(string file) => File.WriteAllBytes(file, ToPng());

    /// <summary>The picture as the bytes of a PNG file.</summary>
    public byte[] ToPng()
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;   // bits per channel
        header[9] = 6;   // truecolour with alpha
        Chunk(stream, "IHDR", header);

        var raw = new byte[((width * 4) + 1) * height];
        for (var y = 0; y < height; y++)
        {
            raw[y * ((width * 4) + 1)] = 0;
            pixels.AsSpan(y * width * 4, width * 4).CopyTo(raw.AsSpan((y * ((width * 4) + 1)) + 1));
        }

        using var deflated = new MemoryStream();
        using (var zlib = new ZLibStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        Chunk(stream, "IDAT", deflated.ToArray());
        Chunk(stream, "IEND", []);
        return stream.ToArray();
    }

    /// <summary>The picture as a PNG in a data URI, for putting straight into a page.</summary>
    public string ToDataUri() => $"data:image/png;base64,{Convert.ToBase64String(ToPng())}";

    private static byte Channel(float value) => (byte)Math.Clamp(value * 255f, 0f, 255f);

    private static void Chunk(Stream stream, string kind, ReadOnlySpan<byte> body)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, body.Length);
        stream.Write(length);

        var named = new byte[4 + body.Length];
        Encoding.ASCII.GetBytes(kind).CopyTo(named, 0);
        body.CopyTo(named.AsSpan(4));
        stream.Write(named);

        Span<byte> sum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sum, Crc32.HashToUInt32(named));
        stream.Write(sum);
    }

    private void Blend(int x, int y, float r, float g, float b, float a)
    {
        if (x < 0 || y < 0 || x >= width || y >= height || a <= 0f)
        {
            return;
        }

        if (clip is { } box && (x < box.X || y < box.Y || x >= box.Z || y >= box.W))
        {
            return;
        }

        var at = ((y * width) + x) * 4;
        var over = Math.Clamp(a, 0f, 1f);
        var under = pixels[at + 3] / 255f * (1f - over);
        var alpha = over + under;
        if (alpha <= 0f)
        {
            return;
        }

        pixels[at] = Channel((((pixels[at] / 255f) * under) + (r * over)) / alpha);
        pixels[at + 1] = Channel((((pixels[at + 1] / 255f) * under) + (g * over)) / alpha);
        pixels[at + 2] = Channel((((pixels[at + 2] / 255f) * under) + (b * over)) / alpha);
        pixels[at + 3] = Channel(alpha);
    }
}

/// <summary>Puts a clip back when the thing that set it is done with it.</summary>
internal sealed class Restore(Action undo) : IDisposable
{
    public void Dispose() => undo();
}
