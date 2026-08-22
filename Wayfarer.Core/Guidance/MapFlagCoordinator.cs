namespace Wayfarer.Core.Guidance;

/// <summary>The player's own flag, as it stood before anything of ours touched it.</summary>
/// <param name="Existed">False when there was no flag at all — restoring then means clearing.</param>
/// <param name="X">The flag's stored X, in the same space the game's own setter received.</param>
/// <param name="Y">The flag's stored Y (the world Z axis), likewise.</param>
public sealed record MapFlagSnapshot(bool Existed, uint TerritoryId, uint MapId, float X, float Y, uint IconId);

/// <summary>The ONE writer of the game's map flag. Objectives DECLARE that they want to be flagged
/// (<see cref="ObjectiveAffordances.MapFlag"/>); this performs it, and nothing else may.
///
/// The reason for that rule is not tidiness. The game stores exactly one flag — a
/// <c>FixedSizeArray1&lt;FlagMapMarker&gt;</c> — and its setter zeroes the marker count before
/// writing, so setting ours DESTROYS the player's own. That cost is not avoidable, only undoable,
/// which is why this class snapshots the flag the first time it takes ownership and puts it back
/// the moment the objective that wanted it goes away.
///
/// Four rules, in order:
/// <list type="number">
/// <item>Only while an EXPLICIT mode is engaged, and only when the player has left marking on.
/// Nothing ambient, nothing on a timer, nothing on window open.</item>
/// <item>Save once, restore once — on the first flagged objective, and on the last.</item>
/// <item>Change-only cadence: driven by objective identity, never per frame. A live-tracked mob
/// re-emitting its position 60 times a second raises no identity change, so it causes no
/// writes.</item>
/// <item>Ignored where meaningless: an objective with no coordinate (instanced duty, zone-only,
/// unresolved) is not flagged, and that is this coordinator's rule rather than any source's
/// problem.</item>
/// </list>
///
/// It never reads which source produced an objective — that is what keeps it closed to change when
/// a new feature arrives, and substitutable across every feature that already exists.</summary>
/// <param name="arbiter">Subscribed for objective-identity changes, and nothing else.</param>
/// <param name="isEnabled">The player's opt-out, read at every change rather than captured.</param>
/// <param name="readFlag">Snapshots the current flag; null when the game cannot be read safely
/// right now (mid-zone-change, no map agent), in which case ownership is not taken at all.</param>
/// <param name="setFlag">(territory, map, x, y, z) — plants our flag.</param>
/// <param name="restoreFlag">Puts a snapshot back, or clears when it recorded no flag.</param>
public sealed class MapFlagCoordinator(
    IGuidanceArbiter arbiter,
    Func<bool> isEnabled,
    Func<MapFlagSnapshot?> readFlag,
    Action<uint, uint, float, float, float> setFlag,
    Action<MapFlagSnapshot> restoreFlag) : IDisposable
{
    private MapFlagSnapshot? saved;
    private bool owned;

    /// <summary>Starts listening. Separate from construction so the composition root decides when
    /// the coordinator becomes live.</summary>
    public MapFlagCoordinator Start()
    {
        arbiter.OnObjectiveChanged += OnObjectiveChanged;
        return this;
    }

    /// <summary>Unsubscribes and gives the player their flag back — an uninstall or a disabled
    /// module must never leave our marker behind.</summary>
    public void Dispose()
    {
        arbiter.OnObjectiveChanged -= OnObjectiveChanged;
        Release();
    }

    private void OnObjectiveChanged(GuidanceObjective? objective)
    {
        if (!ShouldFlag(objective) || objective!.Destination is not ObjectiveDestination.WorldPoint point)
        {
            Release();
            return;
        }

        if (!owned)
        {
            if (readFlag() is not { } snapshot)
            {
                return; // cannot read the flag safely — do not take ownership of something unknown
            }

            saved = snapshot;
            owned = true;
        }

        setFlag(point.Territory, point.MapId, point.X, point.Y, point.Z);
    }

    private bool ShouldFlag(GuidanceObjective? objective) =>
        isEnabled()
        && objective?.Affordances is { MapFlag: true }
        && arbiter.Engagement == GuidanceEngagement.Engaged;

    private void Release()
    {
        if (!owned)
        {
            return;
        }

        owned = false;
        if (saved is { } snapshot)
        {
            restoreFlag(snapshot);
        }

        saved = null;
    }
}
