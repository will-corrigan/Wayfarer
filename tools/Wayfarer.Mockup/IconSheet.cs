using System.Numerics;
using Lumina;

namespace Wayfarer.Mockup;

/// <summary>A run of the game's icons laid out in a grid with their numbers under them, for
/// choosing one by looking at it rather than by guessing what a number is a picture of.</summary>
internal static class IconSheet
{
    private const int Across = 12;
    private const int Cell = 48;
    private const int Label = 14;

    public static Canvas Draw(GameData game, uint first, int count)
    {
        var down = (count + Across - 1) / Across;
        var canvas = new Canvas(Across * Cell, down * (Cell + Label));
        canvas.Clear(new Vector4(0.10f, 0.11f, 0.13f, 1f));

        var face = GameFont.Axis(game, 12);
        var pale = new Vector4(0.9f, 0.88f, 0.82f, 1f);

        for (var i = 0; i < count; i++)
        {
            var id = first + (uint)i;
            var x = i % Across * Cell;
            var y = i / Across * (Cell + Label);

            try
            {
                var art = Picture.From(game, $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}.tex");
                canvas.Stretch(art, x + 8, y + 8, 32, 32);
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            face.Write(canvas, id.ToString(), x + 4, y + Cell - 4, pale);
        }

        return canvas;
    }
}
