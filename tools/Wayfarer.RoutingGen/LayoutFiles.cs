using System.Globalization;
using System.Net;
using Lumina;
using Lumina.Data.Files;

namespace Wayfarer.RoutingGen;

/// <summary>Where a zone's layout files come from: the game install when it has them, and
/// otherwise the xiviewer.app mirror of the game's files, kept in a cache on disk.
///
/// <para>An install only holds the expansions its owner has. The sheets describe every zone of
/// every expansion, so a graph generated from an install without Endwalker has Endwalker's
/// aetherytes but none of their heights. The mirror serves any game file by its path.</para>
///
/// <para>The cache lives in <c>WAYFARER_LAYOUT_CACHE</c>, or <c>~/.cache/wayfarer/layouts</c>. A file
/// the mirror says it does not have is remembered as missing, so it is asked for once. Any other
/// failure stops the run: a mirror having a bad minute is not a file that does not exist, and a
/// graph built through one would quietly lose that zone's doors and heights.</para></summary>
internal sealed class LayoutFiles : IDisposable
{
    private const string Mirror = "https://xiviewer.app/api/global/latest/file/";
    private const string MissingSuffix = ".missing";

    /// <summary>What a missing marker holds when the mirror really said it has no such file. An
    /// older run also wrote markers for failures that were not that, which are asked again.</summary>
    private static readonly string NotFound = ((int)HttpStatusCode.NotFound).ToString(CultureInfo.InvariantCulture);

    private readonly GameData game;
    private readonly string cache;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public LayoutFiles(GameData game)
    {
        this.game = game;
        cache = Environment.GetEnvironmentVariable("WAYFARER_LAYOUT_CACHE") is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "wayfarer", "layouts");
    }

    /// <summary>How many files came from the mirror, this run or an earlier one.</summary>
    public int Mirrored { get; private set; }

    /// <inheritdoc/>
    public void Dispose() => http.Dispose();

    /// <summary>A layout file, or null when neither the install nor the mirror has it.</summary>
    public LgbFile? Get(string path)
    {
        if (game.FileExists(path))
        {
            return game.GetFile<LgbFile>(path);
        }

        var local = Path.Combine(cache, path);
        var missing = local + MissingSuffix;
        if (File.Exists(missing))
        {
            if (string.Equals(File.ReadAllText(missing).Trim(), NotFound, StringComparison.Ordinal))
            {
                return null;
            }

            File.Delete(missing);
        }

        if (!File.Exists(local) && !Fetch(path, local))
        {
            return null;
        }

        Mirrored++;
        return game.GetFileFromDisk<LgbFile>(local, path);
    }

    private bool Fetch(string path, string local)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        using var response = http.GetAsync(new Uri(Mirror + path + "/")).GetAwaiter().GetResult();
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            File.WriteAllText(local + MissingSuffix, NotFound);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"the mirror answered {(int)response.StatusCode} for {path}; run again once it is back, so the graph does not ship without that zone's layout.", null, response.StatusCode);
        }

        File.WriteAllBytes(local, response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
        return true;
    }
}
