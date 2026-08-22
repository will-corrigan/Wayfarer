using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer.Spike;

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. Experiment 3: drive the game's
/// real flag marker and observe what comes with it (map pin, minimap pin, minimap-rim direction
/// indicator, distance), plus what it costs.
///
/// The cost is settled in the source and reconfirmed here at runtime: the flag is backed by
/// <c>FixedSizeArray1&lt;FlagMapMarker&gt;</c>, so exactly one can exist, and the managed
/// <c>SetFlagMapMarker</c> overload zeroes <c>FlagMarkerCount</c> before calling the native
/// function — meaning setting ours unconditionally destroys the player's own. This class therefore
/// snapshots the existing flag first and can put it back.</summary>
internal sealed unsafe class SpikeFlagMarker(IObjectTable objects, IPluginLog log)
{
    private const uint DefaultFlagIcon = 0xEC91;
    private const float TestOffset = 30.0f;

    private (uint TerritoryId, uint MapId, float X, float Y, uint IconId)? savedFlag;
    private bool savedFlagExisted;
    private bool weSetTheFlag;

    public string LastObservation { get; private set; } = "not run yet";

    /// <summary>Snapshots the player's flag (if any), then plants ours 30 units east of the player
    /// in the current zone so the direction indicator has somewhere to point.</summary>
    public string Set()
    {
        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return Observe("AgentMap unavailable.");
        }

        var player = objects.LocalPlayer;
        if (player is null)
        {
            return Observe("No local player.");
        }

        SnapshotExistingFlag(agent);

        var target = player.Position + new Vector3(TestOffset, 0.0f, 0.0f);
        agent->SetFlagMapMarker(agent->CurrentTerritoryId, agent->CurrentMapId, target, DefaultFlagIcon);
        weSetTheFlag = true;

        var before = savedFlagExisted
            ? $"player had a flag at ({savedFlag!.Value.X:F1}, {savedFlag.Value.Y:F1}) in territory {savedFlag.Value.TerritoryId} — it has been OVERWRITTEN"
            : "player had no flag set";

        return Observe($"Flag set at world ({target.X:F1}, {target.Z:F1}) in territory {agent->CurrentTerritoryId}, map {agent->CurrentMapId}. Before: {before}. FlagMarkerCount now {agent->FlagMarkerCount}.");
    }

    /// <summary>Clears the flag the way the game itself does — the count is the only thing that
    /// makes the marker live, so zeroing it is a complete removal.</summary>
    public string Clear()
    {
        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return Observe("AgentMap unavailable.");
        }

        agent->FlagMarkerCount = 0;
        weSetTheFlag = false;
        return Observe($"Flag cleared (FlagMarkerCount = {agent->FlagMarkerCount}).");
    }

    /// <summary>Puts the player's own flag back from the snapshot taken in <see cref="Set"/>. This
    /// is the mitigation any shipped feature would need: the clobbering is not avoidable, only
    /// undoable.</summary>
    public string Restore()
    {
        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return Observe("AgentMap unavailable.");
        }

        if (savedFlag is not { } saved)
        {
            return Observe("Nothing snapshotted — set the spike flag first.");
        }

        if (!savedFlagExisted)
        {
            agent->FlagMarkerCount = 0;
            weSetTheFlag = false;
            return Observe("The player had no flag before, so restoring means clearing.");
        }

        agent->SetFlagMapMarker(saved.TerritoryId, saved.MapId, saved.X, saved.Y, saved.IconId);
        weSetTheFlag = false;
        return Observe($"Restored the player's flag at ({saved.X:F1}, {saved.Y:F1}) in territory {saved.TerritoryId}.");
    }

    /// <summary>Opens the map on the flag, which is also the game's own "where do I go" idiom (the
    /// scenario guide does exactly this). Useful for confirming the pin actually landed.</summary>
    public string OpenMap()
    {
        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return Observe("AgentMap unavailable.");
        }

        agent->OpenMap(agent->CurrentMapId, agent->CurrentTerritoryId);
        return Observe("Map opened on the current zone.");
    }

    public string Status()
    {
        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return "AgentMap unavailable.";
        }

        var live = agent->FlagMarkerCount is not 0
            ? $"flag live at ({agent->FlagMapMarkers[0].XFloat:F1}, {agent->FlagMapMarkers[0].YFloat:F1}) territory {agent->FlagMapMarkers[0].TerritoryId} icon {agent->FlagMapMarkers[0].MapMarker.IconId}"
            : "no flag set";

        return $"{live}; spike owns it: {weSetTheFlag}; snapshot held: {savedFlag is not null}.";
    }

    private void SnapshotExistingFlag(AgentMap* agent)
    {
        // Only snapshot a flag that is not already ours, otherwise a second /wayspike flag would
        // overwrite the player's saved flag with our own test position.
        if (weSetTheFlag)
        {
            return;
        }

        savedFlagExisted = agent->FlagMarkerCount is not 0;
        ref var marker = ref agent->FlagMapMarkers[0];
        savedFlag = savedFlagExisted
            ? (marker.TerritoryId, marker.MapId, marker.XFloat, marker.YFloat, marker.MapMarker.IconId)
            : (0u, 0u, 0f, 0f, DefaultFlagIcon);
    }

    private string Observe(string message)
    {
        LastObservation = message;
        log.Information($"Spike flag: {message}");
        return message;
    }
}
