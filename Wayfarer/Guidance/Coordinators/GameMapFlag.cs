using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Wayfarer.Core.Guidance;

namespace Wayfarer.Guidance.Coordinators;

/// <summary>The game half of the map-flag affordance: reads, writes and restores
/// <c>AgentMap</c>'s single flag marker. All policy — when to take the flag, when to give it back,
/// whose objective may ask — lives in <see cref="MapFlagCoordinator"/>; this only knows how.
///
/// Verified facts this is built on:
/// <list type="bullet">
/// <item>Storage is <c>FixedSizeArray1&lt;FlagMapMarker&gt;</c> at a fixed offset, immediately
/// followed by the warp-marker array. There is physically ONE flag.</item>
/// <item>The managed setter zeroes <c>FlagMarkerCount</c> before calling the native function, so
/// planting ours destroys the player's.</item>
/// <item>The <c>Vector3</c> overload passes raw world X and Z (rounded to 3dp) and discards Y; the
/// native function does the map conversion. <c>XFloat</c>/<c>YFloat</c> are therefore in the same
/// space, which is what makes restoring a snapshot correct.</item>
/// <item>There is no clear function: zeroing <c>FlagMarkerCount</c> is the game's own idiom
/// (<c>AgentMap.OpenMap</c> tests exactly that field for "is there a flag").</item>
/// </list>
/// The default icon id 60561 is the game's own, taken from the setter's signature.</summary>
internal sealed unsafe class GameMapFlag(IClientState clientState, IPluginLog log)
{
    private const uint DefaultFlagIcon = 60561;

    // The flag is set and restored on every objective change, so a repeatable fault here would
    // write a line per objective. Once each is enough to diagnose it.
    private bool loggedSetFailure;
    private bool loggedRestoreFailure;

    /// <summary>Snapshots the player's current flag, or null when it cannot be read safely: no map
    /// agent, mid-zone-transition (a flag planted then would land on the wrong map), or PvP. A null
    /// here means the coordinator declines to take ownership at all, which is the conservative
    /// outcome.</summary>
    public MapFlagSnapshot? Read()
    {
        var agent = AgentMap.Instance();
        if (agent is null || agent->CurrentTerritoryId == 0 || clientState.IsPvP)
        {
            return null;
        }

        ref var marker = ref agent->FlagMapMarkers[0];
        var existed = agent->FlagMarkerCount is not 0;
        return new MapFlagSnapshot(
            existed,
            existed ? marker.TerritoryId : 0u,
            existed ? marker.MapId : 0u,
            existed ? marker.XFloat : 0f,
            existed ? marker.YFloat : 0f,
            existed ? marker.MapMarker.IconId : DefaultFlagIcon);
    }

    /// <summary>Plants the flag on an objective. Deliberately does NOT open the map: the map window
    /// popping open every time a target dies mid-chain would be intolerable, and the flag alone
    /// already gives the map pin, the minimap pin and the compass marker.</summary>
    public void Set(uint territory, uint mapId, float x, float y, float z)
    {
        var agent = AgentMap.Instance();
        if (agent is null || clientState.IsPvP)
        {
            return;
        }

        try
        {
            agent->SetFlagMapMarker(territory, mapId, new Vector3(x, y, z), DefaultFlagIcon);
        }
        catch (Exception ex)
        {
            if (!loggedSetFailure)
            {
                loggedSetFailure = true;
                const string message =
                    "Wayfarer guidance: the objective could not be flagged on the map, so there will be no map "
                    + "pin, minimap pin or compass marker for it. The readout and its arrow are unaffected. "
                    + "Reported once.";
                log.Warning(ex, message);
            }
        }
    }

    /// <summary>Puts the player's own flag back, or clears ours when they had none.</summary>
    public void Restore(MapFlagSnapshot snapshot)
    {
        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return;
        }

        try
        {
            if (!snapshot.Existed)
            {
                agent->FlagMarkerCount = 0;
                return;
            }

            agent->SetFlagMapMarker(snapshot.TerritoryId, snapshot.MapId, snapshot.X, snapshot.Y, snapshot.IconId);
        }
        catch (Exception ex)
        {
            if (!loggedRestoreFailure)
            {
                loggedRestoreFailure = true;
                const string message =
                    "Wayfarer guidance: your own map flag could not be put back after a route ended, so "
                    + "Wayfarer's flag may be left where yours was. Set it again by clicking the map. "
                    + "Reported once.";
                log.Warning(ex, message);
            }
        }
    }
}
