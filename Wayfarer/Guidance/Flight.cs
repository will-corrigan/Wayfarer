using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Wayfarer.Guidance;

/// <summary>Where the player can fly: a zone whose aether currents they have all attuned. Routing
/// takes the height there as flown, not climbed. A city has no currents at all, so nobody flies in
/// one.</summary>
internal sealed unsafe class Flight(IDataManager data)
{
    /// <summary>Each zone's set of aether currents, by the zone, read from the sheets as asked.</summary>
    private readonly Dictionary<uint, uint> currentsOf = [];

    /// <summary>Whether the player can fly in a zone. Not able is the answer for a zone with no
    /// currents and while the game has no state yet. Game thread only.</summary>
    public bool CanFly(uint territory)
    {
        if (!currentsOf.TryGetValue(territory, out var currents))
        {
            currents = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory)?.AetherCurrentCompFlgSet.RowId ?? 0;
            currentsOf[territory] = currents;
        }

        var player = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
        return currents != 0 && player != null && player->IsAetherCurrentZoneComplete(currents);
    }
}
