using Lumina;
using Lumina.Data.Files;

// Writes game icons out as PNG so they can be looked at. usage: icondump <first> [count] [outDir]
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: icondump <firstIconId> [count] [outDir]  |  icondump --path <ui/uld/Name.tex> [outDir]");
    return 1;
}

// A named picture rather than a numbered icon, for looking at what a window is built from.
if (string.Equals(args[0], "--path", StringComparison.Ordinal))
{
    var only = args[1];
    var into = args.Length > 2 ? args[2] : ".";
    Directory.CreateDirectory(into);
    var named = new GameData(Sqpack()).GetFile<TexFile>(only);
    if (named is null)
    {
        Console.WriteLine($"{only}: no such picture");
        return 1;
    }

    var made = Path.Combine(into, Path.GetFileNameWithoutExtension(only) + ".png");
    WritePng(made, named.Header.Width, named.Header.Height, Straighten(named.ImageData));
    Console.WriteLine($"{only}  {named.Header.Width}x{named.Header.Height}  -> {made}");
    return 0;
}

var first = uint.Parse(args[0]);
var count = args.Length > 1 ? int.Parse(args[1]) : 1;
var outDir = args.Length > 2 ? args[2] : ".";
Directory.CreateDirectory(outDir);

var game = new GameData(Sqpack());
for (var id = first; id < first + count; id++)
{
    var path = $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}.tex";
    var tex = game.GetFile<TexFile>(path);
    if (tex is null)
    {
        Console.WriteLine($"{id}  (no such icon)");
        continue;
    }

    var rgba = Straighten(tex.ImageData);
    var file = Path.Combine(outDir, $"{id}.png");
    WritePng(file, tex.Header.Width, tex.Header.Height, rgba);
    Console.WriteLine($"{id}  {tex.Header.Width}x{tex.Header.Height}  -> {file}");
}

return 0;

// A PNG is a signature, a header, the pixels deflated with a filter byte per row, and an end mark.
// Writing one by hand keeps this tool to the one package it already has.
static void WritePng(string file, int width, int height, ReadOnlySpan<byte> rgba)
{
    using var stream = File.Create(file);
    stream.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

    Span<byte> header = stackalloc byte[13];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
    header[8] = 8;   // bits per channel
    header[9] = 6;   // truecolour with alpha
    Chunk(stream, "IHDR", header);

    var raw = new byte[(width * 4 + 1) * height];
    for (var y = 0; y < height; y++)
    {
        raw[y * (width * 4 + 1)] = 0;
        rgba.Slice(y * width * 4, width * 4).CopyTo(raw.AsSpan((y * (width * 4 + 1)) + 1));
    }

    using var deflated = new MemoryStream();
    using (var zlib = new System.IO.Compression.ZLibStream(deflated, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
    {
        zlib.Write(raw);
    }

    Chunk(stream, "IDAT", deflated.ToArray());
    Chunk(stream, "IEND", []);
}

static void Chunk(Stream stream, string kind, ReadOnlySpan<byte> body)
{
    Span<byte> length = stackalloc byte[4];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, body.Length);
    stream.Write(length);

    var named = new byte[4 + body.Length];
    System.Text.Encoding.ASCII.GetBytes(kind).CopyTo(named, 0);
    body.CopyTo(named.AsSpan(4));
    stream.Write(named);

    Span<byte> sum = stackalloc byte[4];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(sum, System.IO.Hashing.Crc32.HashToUInt32(named));
    stream.Write(sum);
}

// The game stores its pictures blue first. Reading them as though red came first is how a gold
// star comes out blue, so the two ends of each pixel are swapped on the way out.
static byte[] Straighten(ReadOnlySpan<byte> pixels)
{
    var rgba = new byte[pixels.Length];
    for (var i = 0; i < pixels.Length; i += 4)
    {
        (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (pixels[i + 2], pixels[i + 1], pixels[i], pixels[i + 3]);
    }

    return rgba;
}

static string Sqpack() =>
    Environment.GetEnvironmentVariable("SQPACK")
    ?? throw new InvalidOperationException("set SQPACK to the game sqpack folder (or a copy of ffxiv/0a0000.win32.*)");
