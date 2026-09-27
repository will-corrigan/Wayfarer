using System.Text;
using Wayfarer.Modules.Treasure;

namespace Wayfarer.Tests;

/// <summary>Reading a chest's metal from the names of its materials, as the game names them.</summary>
public class TreasureTiersTests
{
    private const string Dummy = "bgcommon/world/tbx/049/material/lod_dummy1a.mtrl";

    [Fact]
    public void Dou_is_bronze()
    {
        Assert.Equal(TreasureTier.Bronze, TreasureTiers.FromMaterials([Dummy, "bgcommon/world/tbx/001/material/w_tbx_001_dou1a.mtrl"]));
    }

    [Fact]
    public void Gin_is_silver()
    {
        Assert.Equal(TreasureTier.Silver, TreasureTiers.FromMaterials([Dummy, "bgcommon/world/tbx/073/material/w_tbx_073_gin1a.mtrl"]));
    }

    [Fact]
    public void Kin_is_gold()
    {
        Assert.Equal(TreasureTier.Gold, TreasureTiers.FromMaterials([Dummy, "bgcommon/world/tbx/074/material/w_tbx_074_kin1a.mtrl"]));
    }

    [Fact]
    public void A_metal_named_without_a_number_still_counts()
    {
        Assert.Equal(TreasureTier.Gold, TreasureTiers.FromMaterials(["bgcommon/world/tbx/057/material/w_tbx_057_kin.mtrl"]));
        Assert.Equal(TreasureTier.Gold, TreasureTiers.FromMaterials(["bgcommon/world/tbx/035/material/w_tbx_035_kina.mtrl"]));
    }

    [Fact]
    public void A_scene_naming_several_metals_is_not_known()
    {
        Assert.Equal(
            TreasureTier.Unknown,
            TreasureTiers.FromMaterials(["bgcommon/world/tbx/036/material/w_tbx_036_doua.mtrl", "bgcommon/world/tbx/037/material/w_tbx_037_gina.mtrl", "bgcommon/world/tbx/038/material/w_tbx_038_kina.mtrl"]));
    }

    [Fact]
    public void The_same_metal_twice_is_still_that_metal()
    {
        Assert.Equal(TreasureTier.Silver, TreasureTiers.FromMaterials(["a/w_tbx_069_gin1a.mtrl", "b/w_tbx_069_gin1a.mtrl"]));
    }

    [Fact]
    public void Materials_that_name_no_metal_leave_it_unknown()
    {
        Assert.Equal(TreasureTier.Unknown, TreasureTiers.FromMaterials([Dummy]));
    }

    [Fact]
    public void A_metal_word_elsewhere_in_the_path_is_not_the_material()
    {
        Assert.Equal(TreasureTier.Unknown, TreasureTiers.FromMaterials(["bg/ffxiv/gin_town/material/wall1a.mtrl"]));
    }

    [Fact]
    public void Paths_are_found_among_a_files_bytes()
    {
        var file = Encoding.ASCII.GetBytes("\0\u0001bgcommon/world/tbx/002/bgparts/w_tbx_002_01a.mdl\0\u0007junk\0bgcommon/world/tbx/002/bgparts/w_tbx_002_01b.mdl\0");

        Assert.Equal(
            ["bgcommon/world/tbx/002/bgparts/w_tbx_002_01a.mdl", "bgcommon/world/tbx/002/bgparts/w_tbx_002_01b.mdl"],
            [.. TreasureTiers.PathsIn(file, "mdl")],
            StringComparer.Ordinal);
    }
}
