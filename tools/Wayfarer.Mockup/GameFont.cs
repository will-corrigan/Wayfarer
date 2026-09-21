using System.Numerics;
using Dalamud.Interface.GameFonts;
using Lumina;

namespace Wayfarer.Mockup;

/// <summary>One of the game's own faces at one of its own sizes, drawn from the same files the
/// game draws it from: a table saying where each letter sits in a picture, and the pictures. The
/// pictures hold coverage rather than colour — four letters share a pixel, one to a channel — so a
/// letter is drawn by reading one channel and laying the wanted colour under it.</summary>
internal sealed class GameFont
{
    /// <summary>The sizes the game keeps of its interface face. Anything else is drawn in the
    /// nearest of these, because a glyph drawn from a picture does not scale without going soft.</summary>
    private static readonly int[] AxisSizes = [12, 14, 18, 36];

    private readonly FdtReader table;
    private readonly Picture[] pages;

    private GameFont(FdtReader table, Picture[] pages)
    {
        this.table = table;
        this.pages = pages;
    }

    /// <summary>How far one line of this face sits below the last.</summary>
    public int LineHeight => table.FontHeader.LineHeight;

    /// <summary>Loads the interface face at the nearest size the game keeps to the one asked for.</summary>
    public static GameFont Axis(GameData game, int size)
    {
        var nearest = AxisSizes.MinBy(kept => Math.Abs(kept - size));
        return Named(game, $"AXIS_{nearest}");
    }

    /// <summary>Loads a face by the name of its table, for the faces the game uses for headings.</summary>
    public static GameFont Named(GameData game, string face)
    {
        var file = game.GetFile($"common/font/{face}.fdt") ?? throw new FileNotFoundException($"no such face: {face}");
        var table = new FdtReader(file.Data);

        // A face spans as many pictures as it needs; each holds four pages, one to a channel.
        var wanted = table.Glyphs.Count == 0 ? 0 : table.Glyphs.Max(g => g.TextureFileIndex) + 1;
        var pages = new Picture[wanted];
        for (var i = 0; i < wanted; i++)
        {
            pages[i] = Picture.Raw(game, $"common/font/font{i + 1}.tex");
        }

        return new GameFont(table, pages);
    }

    /// <summary>How wide a line of words is in this face, kerning included.</summary>
    public float Measure(string words)
    {
        var across = 0f;
        for (var i = 0; i < words.Length; i++)
        {
            var glyph = table.GetGlyph(words[i]);
            across += glyph.AdvanceWidth;
            if (i + 1 < words.Length)
            {
                across += table.GetDistance(words[i], words[i + 1]);
            }
        }

        return across;
    }

    /// <summary>Draws a line of words with its left edge at <paramref name="x"/> and the top of its
    /// line at <paramref name="y"/>, and answers where the next word would start.</summary>
    public float Write(Canvas canvas, string words, float x, float y, Vector4 colour)
    {
        var pen = x;
        for (var i = 0; i < words.Length; i++)
        {
            var glyph = table.GetGlyph(words[i]);
            Stamp(canvas, glyph, pen, y + glyph.CurrentOffsetY, colour);
            pen += glyph.AdvanceWidth;
            if (i + 1 < words.Length)
            {
                pen += table.GetDistance(words[i], words[i + 1]);
            }
        }

        return pen;
    }

    /// <summary>Breaks a line of words to a width the way the game's own text nodes do — at spaces,
    /// never inside a word — and answers the lines it made.</summary>
    public List<string> Wrap(string words, float width)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var tried = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && Measure(tried) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = tried;
            }
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Every letter of the face packed into one picture, white with the coverage kept in
    /// the alpha, so a page can colour the letters itself. What comes back beside it says where
    /// each letter sits in the picture and how far the pen moves after it.</summary>
    public (Canvas Sheet, List<GlyphPlace> Places) Atlas()
    {
        const int Across = 1024;
        const int Pad = 1;

        var letters = new List<FdtReader.FontTableEntry>();
        for (var code = ' '; code <= '~'; code++)
        {
            letters.Add(table.GetGlyph(code));
        }

        // Laid out in rows, each as tall as the tallest letter on it, so the sheet is only as big
        // as the letters need rather than a square of mostly nothing.
        var places = new List<GlyphPlace>();
        var (penX, penY, rowTall) = (Pad, Pad, 0);
        for (var i = 0; i < letters.Count; i++)
        {
            var glyph = letters[i];
            if (penX + glyph.BoundingWidth + Pad > Across)
            {
                (penX, penY, rowTall) = (Pad, penY + rowTall + Pad, 0);
            }

            places.Add(new GlyphPlace(
                (char)(' ' + i), penX, penY, glyph.BoundingWidth, glyph.BoundingHeight, glyph.CurrentOffsetY, glyph.AdvanceWidth));
            penX += glyph.BoundingWidth + Pad;
            rowTall = Math.Max(rowTall, glyph.BoundingHeight);
        }

        var sheet = new Canvas(Across, penY + rowTall + Pad);
        for (var i = 0; i < letters.Count; i++)
        {
            var glyph = letters[i];
            var place = places[i];
            var page = pages[glyph.TextureFileIndex];
            var channel = glyph.TextureChannelByteIndex;

            for (var y = 0; y < glyph.BoundingHeight; y++)
            {
                for (var x = 0; x < glyph.BoundingWidth; x++)
                {
                    var sx = glyph.TextureOffsetX + x;
                    var sy = glyph.TextureOffsetY + y;
                    if (sx >= page.Width || sy >= page.Height)
                    {
                        continue;
                    }

                    var coverage = page.Pixels[(((sy * page.Width) + sx) * 4) + channel] / 255f;
                    sheet.Ink(place.X + x, place.Y + y, coverage, Vector4.One);
                }
            }
        }

        return (sheet, places);
    }

    private void Stamp(Canvas canvas, FdtReader.FontTableEntry glyph, float x, float y, Vector4 colour)
    {
        var page = pages[glyph.TextureFileIndex];
        var channel = glyph.TextureChannelByteIndex;
        var left = (int)MathF.Round(x);
        var top = (int)MathF.Round(y);

        for (var py = 0; py < glyph.BoundingHeight; py++)
        {
            var sy = glyph.TextureOffsetY + py;
            if (sy >= page.Height)
            {
                break;
            }

            for (var px = 0; px < glyph.BoundingWidth; px++)
            {
                var sx = glyph.TextureOffsetX + px;
                if (sx >= page.Width)
                {
                    break;
                }

                var coverage = page.Pixels[((((sy * page.Width) + sx) * 4) + channel)] / 255f;
                canvas.Ink(left + px, top + py, coverage, colour);
            }
        }
    }
}

/// <summary>Where one letter sits in a packed sheet, and what the pen does around it.</summary>
/// <param name="Ch">The letter.</param>
/// <param name="X">Its left edge in the sheet.</param>
/// <param name="Y">Its top edge in the sheet.</param>
/// <param name="W">How wide its picture is.</param>
/// <param name="H">How tall its picture is.</param>
/// <param name="Top">How far below the line its picture starts.</param>
/// <param name="Advance">How far the pen moves after drawing it.</param>
internal sealed record GlyphPlace(char Ch, int X, int Y, int W, int H, int Top, int Advance);
