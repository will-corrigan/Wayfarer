using System.Numerics;
using Lumina;
using Lumina.Excel.Sheets;

namespace Wayfarer.Mockup;

/// <summary>The game's own colours, read from the same sheet the plugin reads them from. Every
/// colour is kept once per look the game offers; <see cref="OnPale"/> takes the one meant for a
/// pale background, which is what a page of parchment is.</summary>
internal sealed class Palette(GameData game)
{
    private const uint ListTextRow = 8;

    /// <summary>The colour the game writes its lists in, on parchment.</summary>
    public Vector4 Ink => OnPale(ListTextRow);

    /// <summary>The same, dropped back, for the words under a heading.</summary>
    public Vector4 FaintInk => Ink with { W = 0.72f };

    /// <summary>Every colour the game keeps, in row order.</summary>
    public IEnumerable<UIColor> All() => game.GetExcelSheet<UIColor>()
        ?? throw new InvalidOperationException("no UIColor sheet");

    public Vector4 OnPale(uint rowId) => Unpack(Row(rowId).Light);

    /// <summary>The same colour as the game would use for it on one of its own dark windows.</summary>
    public Vector4 OnDark(uint rowId) => Unpack(Row(rowId).Dark);

    private static Vector4 Unpack(uint packed) => new(
        ((packed >> 24) & 0xFF) / 255f,
        ((packed >> 16) & 0xFF) / 255f,
        ((packed >> 8) & 0xFF) / 255f,
        (packed & 0xFF) / 255f);

    private UIColor Row(uint rowId) => game.GetExcelSheet<UIColor>()?.GetRowOrDefault(rowId)
        ?? throw new InvalidOperationException($"no UIColor row {rowId}");
}
