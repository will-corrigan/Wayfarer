using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Lumina;

namespace Wayfarer.Mockup;

/// <summary>Serves the design bench to a browser on this machine.
///
/// <para>It is a server rather than one file with its pictures inside it for two reasons. There
/// are three thousand pieces and more than three hundred pictures, which is far too much to carry
/// in a page; and a page opened straight off the disk is not allowed to read what a picture it
/// drew actually contains, so it could never save what you arranged on it. Served, it can.</para>
///
/// <para>Nothing is decoded until it is asked for, and what is decoded is kept, so the bench opens
/// at once and gets quicker as it is used.</para></summary>
internal sealed class Bench(GameData game, Parts.Catalogue catalogue)
{
    /// <summary>Which icons to offer: the game's interface icons, and the marks it hangs over the
    /// heads of people worth talking to.</summary>
    private static readonly (uint First, int Count)[] IconRuns = [(61000, 1000), (62000, 600), (71000, 400)];

    /// <summary>The sizes of the game's interface face worth offering.</summary>
    private static readonly int[] FaceSizes = [12, 14, 18, 36];

    private readonly Dictionary<string, byte[]> kept = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    public static void Run(GameData game, Parts.Catalogue catalogue, int port)
    {
        var bench = new Bench(game, catalogue);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        Console.WriteLine($"design bench on http://localhost:{port}/  ({catalogue.Windows.Count} windows, {catalogue.Pieces.Count} pieces)");
        Console.WriteLine("ctrl-c to stop it.");

        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                return;
            }

            try
            {
                bench.Answer(context);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{context.Request.RawUrl}: {ex.Message}");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }
    }

    private void Answer(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var query = context.Request.QueryString;

        switch (path)
        {
            case "/":
                Send(context, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(DesignPage.Template));
                return;

            case "/catalogue.json":
                Send(context, "application/json", Encoding.UTF8.GetBytes(Catalogue()));
                return;

            case "/tex":
                Send(context, "image/png", Texture(query["p"] ?? string.Empty));
                return;

            case "/icon":
                Send(context, "image/png", Icon(uint.Parse(query["id"] ?? "0")));
                return;

            case "/face":
                Send(context, "image/png", Face(int.Parse(query["size"] ?? "14")));
                return;

            default:
                context.Response.StatusCode = 404;
                return;
        }
    }

    private static void Send(HttpListenerContext context, string type, byte[] body)
    {
        context.Response.ContentType = type;
        context.Response.ContentLength64 = body.Length;

        // The bench is opened over and over while a page is being designed; the art behind it never
        // changes between openings, so the browser is told it may keep what it already has.
        context.Response.Headers["Cache-Control"] = "max-age=86400";
        context.Response.OutputStream.Write(body);
    }

    private byte[] Remember(string key, Func<byte[]> make)
    {
        lock (gate)
        {
            if (kept.TryGetValue(key, out var already))
            {
                return already;
            }

            var made = make();
            kept[key] = made;
            return made;
        }
    }

    private byte[] Texture(string path) => Remember($"tex:{path}", () =>
    {
        var art = Picture.From(game, path);
        var canvas = new Canvas(art.Width, art.Height);
        canvas.Draw(art, 0, 0);
        return canvas.ToPng();
    });

    private byte[] Icon(uint id) => Remember($"icon:{id}", () =>
    {
        var art = Picture.From(game, $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}.tex");
        var canvas = new Canvas(art.Width, art.Height);
        canvas.Draw(art, 0, 0);
        return canvas.ToPng();
    });

    private byte[] Face(int size) => Remember($"face:{size}", () => GameFont.Axis(game, size).Atlas().Sheet.ToPng());

    /// <summary>One of the game's colours as a page can use it. The sheet keeps them red first
    /// with the alpha last, which is not how the game's own holder keeps them at runtime — the
    /// sheet is what is being read here.</summary>
    private static string Css(uint packed) => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"rgb({(packed >> 24) & 0xFF} {(packed >> 16) & 0xFF} {(packed >> 8) & 0xFF} / {(packed & 0xFF) / 255f:0.##})");

    /// <summary>What the bench needs to draw its own bin: the windows, the pictures and their
    /// sizes, every rectangle cut out of them, the letters, the colours and the icons that exist.
    /// None of the art itself — that is fetched a piece at a time as the bin is scrolled.</summary>
    private string Catalogue()
    {
        var faces = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var size in FaceSizes)
        {
            var face = GameFont.Axis(game, size);
            var (_, places) = face.Atlas();
            faces[size.ToString()] = new
            {
                lineHeight = face.LineHeight,
                glyphs = places.ToDictionary(
                    place => place.Ch.ToString(),
                    place => new[] { place.X, place.Y, place.W, place.H, place.Top, place.Advance }),
            };
        }

        var icons = new List<uint>();
        foreach (var (first, count) in IconRuns)
        {
            for (var i = 0; i < count; i++)
            {
                var id = first + (uint)i;
                if (game.FileExists($"ui/icon/{id / 1000 * 1000:000000}/{id:000000}.tex"))
                {
                    icons.Add(id);
                }
            }
        }

        // The whole table, both looks, rather than a handful somebody chose. Picking the colour the
        // game itself uses is the point of the bench; a shortlist of five is a way of getting it
        // wrong quietly.
        var palette = new Palette(game);
        var colours = palette.All().Select(row => new
        {
            row = row.RowId,
            dark = Css(row.Dark),
            light = Css(row.Light),
        });

        return JsonSerializer.Serialize(
            new
            {
                windows = catalogue.Windows,
                textures = catalogue.Textures.ToDictionary(t => t.Key, t => new[] { t.Value.Width, t.Value.Height }),
                pieces = catalogue.Pieces.Select(p => new object[] { p.Window, p.Texture, p.U, p.V, p.W, p.H }),
                uses = catalogue.Uses.Select(u => new[] { u.Window, u.Texture }),
                faces,
                icons,
                colours,
            },
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
