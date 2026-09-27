using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace Wayfarer.Modules.Treasure;

/// <summary>Every place a treasure coffer can appear in the zones that have them, read from the
/// zone's own layout file in the player's install.
///
/// <para>No sheet lists them. The zone's <c>planmap.lgb</c> places one treasure object per spot, on a
/// layer of its own; the server decides which are filled. The raid's treasure layers in the same
/// files are underground and left out.</para></summary>
internal sealed class CofferSpots(IDataManager data, IPluginLog log)
{
    /// <summary>The zones with coffers, and the layer holding their field spots.</summary>
    private static readonly Dictionary<uint, string> FieldLayers = new()
    {
        [1252] = "Field_Treasure", // The Occult Crescent: South Horn
        [1346] = "LVD_FLD_treasure", // The Occult Crescent: North Horn
    };

    private readonly Lazy<IReadOnlyList<CofferSpot>> spots = new(() => Read(data, log));

    /// <summary>Every spot, in every zone that has them. The first call reads the layout files.</summary>
    public IReadOnlyList<CofferSpot> All => spots.Value;

    /// <summary>Reads the layout files now, so the map does not pay for them. Safe off the game's thread.</summary>
    public void Warm() => _ = spots.Value;

    private static List<CofferSpot> Read(IDataManager data, IPluginLog log)
    {
        var found = new List<CofferSpot>();
        foreach (var (territory, layerName) in FieldLayers)
        {
            var before = found.Count;
            try
            {
                if (data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory) is not { } zone || zone.Bg.ExtractText() is not { Length: > 0 } bg || !bg.Contains('/', StringComparison.Ordinal))
                {
                    continue;
                }

                var path = $"bg/{bg[..bg.LastIndexOf('/')]}/planmap.lgb";
                if (data.GetFile<LgbFile>(path) is { } layout)
                {
                    foreach (var layer in layout.Layers.Where(layer => string.Equals(layer.Name, layerName, StringComparison.Ordinal)))
                    {
                        foreach (var thing in layer.InstanceObjects.Where(thing => thing.Object is LayerCommon.TreasureInstanceObject))
                        {
                            var at = thing.Transform.Translation;
                            found.Add(new CofferSpot(territory, zone.Map.RowId, new Vector3(at.X, at.Y, at.Z)));
                        }
                    }
                }

                log.Debug($"treasure: {found.Count - before} coffer spots in territory {territory} from {path}.");
            }
            catch (Exception ex)
            {
                // Any failure, not only the expected ones: a patch can change a layout file in ways
                // the reader chokes on, and one zone lost must not lose the other, or stop nearby
                // treasure being shown.
                found.RemoveRange(before, found.Count - before);
                log.Warning(ex, $"treasure: the layout of territory {territory} could not be read, so its coffer spots are not marked.");
            }
        }

        return found;
    }
}
