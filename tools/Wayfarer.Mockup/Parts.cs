using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Lumina;
using Lumina.Data.Files;

namespace Wayfarer.Mockup;

/// <summary>Everything the game's interface is built out of, found rather than guessed at.
///
/// <para>sqpack keeps paths as hashes, so there is no folder to list and a name is either known or
/// not. Two things supply the names. The structs library that comes with Dalamud has a type for
/// every window the game has, named after it, which is a list of what to look for; and each window
/// that turns up carries the paths of its own pictures and the rectangles it cuts out of them,
/// which is where the individual buttons come from. A button's picture is a sheet of every state
/// it has, and it is the window that says which part of the sheet is the button.</para></summary>
internal static class Parts
{
    /// <summary>Names the structs library does not have a type for, because they are pieces of
    /// scenery rather than windows the game opens.</summary>
    private static readonly string[] Extra =
    [
        "Window", "WindowA", "ToolTipS", "ToolTipA", "ListA", "ListB", "ListC", "CheckBoxA",
        "RadioButtonA", "ButtonA", "ButtonB", "ButtonC", "DropDownA", "SliderA", "ScrollBarA",
        "IconA_Frame", "ItemDetail", "CircleButtons", "Cursor", "Achievement", "Orchestrion",
        "Character", "Emote", "Description", "AozNoteBook", "ScenarioTree", "Journal", "Talk",
    ];

    /// <summary>One rectangle a window cuts out of one of its pictures.</summary>
    /// <param name="Window">Which window's list it came from.</param>
    /// <param name="Texture">The picture it is cut from.</param>
    /// <param name="U">Its left edge in that picture.</param>
    /// <param name="V">Its top edge.</param>
    /// <param name="W">How wide it is.</param>
    /// <param name="H">How tall it is.</param>
    internal sealed record Piece(string Window, string Texture, int U, int V, int W, int H);

    /// <summary>What was found: the windows that exist, their pictures with sizes, and every
    /// distinct rectangle cut out of them.</summary>
    /// <param name="Windows">The windows that turned up, in name order.</param>
    /// <param name="Textures">Each picture's path and how big it is.</param>
    /// <param name="Pieces">Every distinct rectangle, in the order the windows listed them.</param>
    internal sealed record Catalogue(
        IReadOnlyList<string> Windows,
        IReadOnlyDictionary<string, (int Width, int Height)> Textures,
        IReadOnlyList<Piece> Pieces,
        IReadOnlyList<(string Window, string Texture)> Uses);

    /// <summary>Every name worth trying: one per window the structs library knows about, plus the
    /// scenery it has no type for.</summary>
    public static IReadOnlyList<string> Candidates(params string[] assemblies)
    {
        var names = new SortedSet<string>(Extra, StringComparer.Ordinal);

        foreach (var assembly in assemblies.Where(File.Exists))
        {
            // Read the names out of the assembly's tables rather than loading it: loading would drag
            // in everything it depends on, and none of that is here or needed to read a list of
            // names. Two heaps are worth reading. The type names give a window per struct; the text
            // the code was written with gives the rest, because a window the game opens is asked for
            // by name in quotes — which is the only place a name like ScenarioTree is written down.
            using var stream = File.OpenRead(assembly);
            using var pe = new PEReader(stream);
            var meta = pe.GetMetadataReader();

            foreach (var handle in meta.TypeDefinitions)
            {
                var name = meta.GetString(meta.GetTypeDefinition(handle).Name);
                if (name.StartsWith("Addon", StringComparison.Ordinal) && name.Length > 5)
                {
                    Offer(names, name["Addon".Length..]);
                }

                Offer(names, name);
            }

            var text = MetadataTokens.UserStringHandle(0);
            while ((text = meta.GetNextHandle(text)) != default)
            {
                Offer(names, meta.GetUserString(text));
            }
        }

        return [.. names];
    }

    /// <summary>Keeps a name only if it could be a file's. Most of what is written down in an
    /// assembly is a sentence, a path or a format string, and asking sqpack about those is a way of
    /// spending a minute to learn nothing.</summary>
    private static void Offer(SortedSet<string> names, string name)
    {
        if (name.Length is < 3 or > 40)
        {
            return;
        }

        foreach (var letter in name)
        {
            if (!char.IsAsciiLetterOrDigit(letter) && letter != '_')
            {
                return;
            }
        }

        names.Add(name);
    }

    /// <summary>Opens every window that is really there and writes down what it is made of.</summary>
    public static Catalogue Harvest(GameData game, IReadOnlyList<string> candidates)
    {
        var windows = new List<string>();
        var textures = new Dictionary<string, (int Width, int Height)>(StringComparer.OrdinalIgnoreCase);
        var pieces = new List<Piece>();
        var uses = new List<(string Window, string Texture)>();

        bool Keep(string path)
        {
            if (textures.ContainsKey(path))
            {
                return true;
            }

            if (game.GetFile<TexFile>(path) is not { } picture)
            {
                return false;
            }

            textures[path] = (picture.Header.Width, picture.Header.Height);
            return true;
        }

        foreach (var name in candidates)
        {
            UldFile? uld;
            try
            {
                uld = game.GetFile<UldFile>($"ui/uld/{name}.uld");
            }
            catch (Exception)
            {
                // A name that hashes onto something that is not a window reads as nonsense. That is
                // what a guessed name looks like when it is wrong, and it is not worth reporting.
                continue;
            }

            // A name with no list of its own may still have a picture. Plenty do: the art is there
            // and its rectangles are cut somewhere else, and bailing out here is why a search for
            // one came back empty while the picture sat in sqpack.
            if (uld is null)
            {
                var only = $"ui/uld/{name}.tex";
                if (Keep(only))
                {
                    windows.Add(name);
                    uses.Add((name, only));
                }

                continue;
            }

            var byId = new Dictionary<uint, string>();
            foreach (var asset in uld.AssetData)
            {
                var path = new string(asset.Path).TrimEnd('\0', ' ');
                if (path.Length == 0)
                {
                    continue;
                }

                if (!Keep(path))
                {
                    continue;
                }

                byId[asset.Id] = path;
                uses.Add((name, path));
            }

            var own = $"ui/uld/{name}.tex";
            if (byId.Count == 0)
            {
                if (!Keep(own))
                {
                    continue;
                }

                windows.Add(name);
                uses.Add((name, own));
                continue;
            }

            if (Keep(own) && !uses.Contains((name, own)))
            {
                uses.Add((name, own));
            }

            windows.Add(name);

            // Kept per window rather than across all of them. A rectangle shared by two windows
            // belongs to both, and dropping the second copy left that window's shelf showing only
            // the sheets its pieces had already been claimed out of.
            var seen = new HashSet<(string, int, int, int, int)>();
            foreach (var group in uld.Parts)
            {
                foreach (var part in group.Parts)
                {
                    if (!byId.TryGetValue(part.TextureId, out var path) || part.W == 0 || part.H == 0)
                    {
                        continue;
                    }

                    if (seen.Add((path, part.U, part.V, part.W, part.H)))
                    {
                        pieces.Add(new Piece(name, path, part.U, part.V, part.W, part.H));
                    }
                }
            }
        }

        return new Catalogue(windows, textures, ImmutableArray.CreateRange(pieces), ImmutableArray.CreateRange(uses));
    }
}
