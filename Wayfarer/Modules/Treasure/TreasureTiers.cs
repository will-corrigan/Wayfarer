using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;

namespace Wayfarer.Modules.Treasure;

/// <summary>Which metal a kind of chest is, read from the game's own files once per kind.
///
/// <para>No sheet says. A loaded chest's id is a <c>Treasure</c> row, whose scene names the chest's
/// models, whose materials are named for the metal in Japanese: <c>dou</c> (bronze), <c>gin</c>
/// (silver), <c>kin</c> (gold), as in <c>w_tbx_002_gin1a.mtrl</c>. The files are only read off the
/// game's thread; until a kind has been read it is <see cref="TreasureTier.Unknown"/>.</para></summary>
internal sealed partial class TreasureTiers(IDataManager data, IPluginLog log)
{
    private readonly ConcurrentDictionary<uint, TreasureTier> known = new();
    private readonly ConcurrentDictionary<uint, byte> reading = new();

    /// <summary>The metal a chest's material names spell, or <see cref="TreasureTier.Unknown"/> when
    /// they name none, or more than one: one scene in the game carries a bronze, a silver and a gold
    /// chest at once, and which is shown is not in the files.</summary>
    public static TreasureTier FromMaterials(IEnumerable<string> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        var metals = materials
            .Select(material => Metal().Match(material))
            .Where(found => found.Success)
            .Select(found => found.Groups["metal"].Value switch
            {
                "dou" => TreasureTier.Bronze,
                "gin" => TreasureTier.Silver,
                _ => TreasureTier.Gold,
            })
            .Distinct()
            .ToList();
        return metals.Count == 1 ? metals[0] : TreasureTier.Unknown;
    }

    /// <summary>The file paths with this extension written in a game file. Scenes and models name the
    /// files they use as plain text among their bytes.</summary>
    public static IEnumerable<string> PathsIn(byte[] file, string extension)
    {
        ArgumentNullException.ThrowIfNull(file);
        var text = Encoding.ASCII.GetString([.. file.Select(b => b is >= 32 and < 127 ? b : (byte)'\n')]);
        return Regex.Matches(text, $@"[\w/\-.]+\.{Regex.Escape(extension)}", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1))
            .Select(found => found.Value)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>The metal of a kind of chest, or <see cref="TreasureTier.Unknown"/> while it is still
    /// being read. Asking for a kind not read yet starts reading it. Any thread.</summary>
    public TreasureTier Of(uint treasure)
    {
        if (known.TryGetValue(treasure, out var tier))
        {
            return tier;
        }

        if (reading.TryAdd(treasure, 0))
        {
            _ = Task.Run(() => known[treasure] = Read(treasure));
        }

        return TreasureTier.Unknown;
    }

    [GeneratedRegex(@"_(?<metal>dou|gin|kin)\d*[a-z]?\.mtrl$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Metal();

    private TreasureTier Read(uint treasure)
    {
        try
        {
            if (data.GetExcelSheet<Lumina.Excel.Sheets.Treasure>().GetRowOrDefault(treasure)?.SGB.ValueNullable?.SgbPath.ExtractText() is not { Length: > 0 } scene
                || data.GetFile(scene) is not { } sceneFile)
            {
                return TreasureTier.Unknown;
            }

            var materials = PathsIn(sceneFile.Data, "mdl")
                .Select(model => data.GetFile(model))
                .OfType<Lumina.Data.FileResource>()
                .SelectMany(model => PathsIn(model.Data, "mtrl"));
            var tier = FromMaterials(materials);
            log.Debug($"treasure: kind {treasure} ({scene}) is {tier}.");
            return tier;
        }
        catch (Exception ex)
        {
            // Any failure leaves the chest drawn as it always was; it must never stop the map.
            log.Debug($"treasure: kind {treasure} could not be read ({ex.GetType().Name}), so its metal is not known.");
            return TreasureTier.Unknown;
        }
    }
}
