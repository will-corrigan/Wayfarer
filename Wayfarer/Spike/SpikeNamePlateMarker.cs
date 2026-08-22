using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Spike;

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. Experiment 2: put the game's
/// own large nameplate marker icon (the one it uses for quest availability) over specific objects.
///
/// Two things matter and are easy to get wrong:
/// <list type="bullet">
/// <item><description><c>MarkerIconId</c> is read and reset by the game EVERY frame, so it has to
/// be written from <c>OnDataUpdate</c> (fires every frame for all handlers), not
/// <c>OnNamePlateUpdate</c> (fires only on "important" changes). <see cref="Mode"/> can be flipped
/// in-game to demonstrate the difference rather than assert it.</description></item>
/// <item><description>The handler objects are valid for one frame only — nothing here may be
/// cached across frames.</description></item>
/// </list></summary>
internal sealed class SpikeNamePlateMarker(
    INamePlateGui namePlates,
    IObjectTable objects,
    IClientState clientState,
    HuntingLogService hunting,
    IUnlockProvider unlocks,
    IPluginLog log) : IDisposable
{
    /// <summary>Icon ids worth trying, in the order the research ranks them: 60094 is the marker
    /// InventoryTools uses for highlighted shop NPCs (the closest working precedent), 61704/61709
    /// are AetherCompass's hunt-rank markers, 60561 is the red flag.</summary>
    private static readonly int[] CandidateIcons = [60094, 61704, 61709, 60561, 71003];

    private readonly HashSet<uint> huntNameIds = [];
    private readonly HashSet<string> giverNames = new(StringComparer.Ordinal);
    private readonly HashSet<ulong> fallbackObjectIds = [];

    private DateTimeOffset lastTargetRefresh = DateTimeOffset.MinValue;
    private bool subscribed;

    internal enum MarkMode
    {
        Off,

        /// <summary>The correct one per the research — fires every frame.</summary>
        DataUpdate,

        /// <summary>The wrong one, kept so the difference is observable rather than assumed.</summary>
        NamePlateUpdate,
    }

    public MarkMode Mode { get; private set; } = MarkMode.Off;

    public int MarkerIconId { get; private set; } = CandidateIcons[0];

    /// <summary>Human-readable description of what is currently being marked, for the readout.</summary>
    public string Summary { get; private set; } = "nothing";

    public void Dispose() => Unsubscribe();

    /// <summary>Off → DataUpdate → NamePlateUpdate → Off. Each transition re-subscribes cleanly and
    /// asks the game to redraw so a stale marker never lingers.</summary>
    public MarkMode CycleMode()
    {
        Unsubscribe();

        Mode = Mode switch
        {
            MarkMode.Off => MarkMode.DataUpdate,
            MarkMode.DataUpdate => MarkMode.NamePlateUpdate,
            _ => MarkMode.Off,
        };

        if (Mode is MarkMode.DataUpdate)
        {
            namePlates.OnDataUpdate += OnUpdate;
            subscribed = true;
        }
        else if (Mode is MarkMode.NamePlateUpdate)
        {
            namePlates.OnNamePlateUpdate += OnUpdate;
            subscribed = true;
        }

        RefreshTargets(force: true);
        namePlates.RequestRedraw();
        log.Information($"Spike nameplates: mode {Mode}, icon {MarkerIconId}, marking {Summary}");
        return Mode;
    }

    /// <summary>Sets the icon id used for every marked plate. Runtime-settable because the right id
    /// is one of the few genuinely open questions and browsing the sheet in-game beats guessing.</summary>
    public void SetIcon(int iconId)
    {
        MarkerIconId = iconId;
        namePlates.RequestRedraw();
        log.Information($"Spike nameplates: icon id set to {iconId}");
    }

    public int NextCandidateIcon()
    {
        var index = Array.IndexOf(CandidateIcons, MarkerIconId);
        SetIcon(CandidateIcons[(index + 1) % CandidateIcons.Length]);
        return MarkerIconId;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        namePlates.OnDataUpdate -= OnUpdate;
        namePlates.OnNamePlateUpdate -= OnUpdate;
        subscribed = false;
        namePlates.RequestRedraw();
    }

    private void OnUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        RefreshTargets(force: false);

        foreach (var handler in handlers)
        {
            if (ShouldMark(handler))
            {
                handler.MarkerIconId = MarkerIconId;
            }
        }
    }

    private bool ShouldMark(INamePlateUpdateHandler handler)
    {
        if (fallbackObjectIds.Contains(handler.GameObjectId))
        {
            return true;
        }

        return handler.NamePlateKind switch
        {
            // Hunting-log targets: BNpcName row id, which lives on IBattleNpc.NameId — NOT DataId,
            // which for a battle NPC is the BNpcBase row id, a different id space entirely.
            NamePlateKind.BattleNpcEnemy or NamePlateKind.BattleNpcFriendly =>
                handler.GameObject is IBattleNpc battleNpc && huntNameIds.Contains(battleNpc.NameId),

            // Unlock quest givers: matched by name because the unlock dataset carries the giver's
            // name and coordinates, not an ENpcResident row id.
            NamePlateKind.EventNpcCompanion =>
                handler.GameObject is { } gameObject && giverNames.Contains(gameObject.Name.TextValue),

            _ => false,
        };
    }

    /// <summary>Recomputes the match sets at most once a second. The per-frame handler must stay
    /// cheap, and neither the hunting page nor the unlock checklist changes faster than this.</summary>
    private void RefreshTargets(bool force)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && now - lastTargetRefresh < TimeSpan.FromSeconds(1))
        {
            return;
        }

        lastTargetRefresh = now;

        huntNameIds.Clear();
        foreach (var monster in hunting.RemainingOnPage)
        {
            huntNameIds.Add(monster.BNpcNameId);
        }

        giverNames.Clear();
        var here = clientState.TerritoryType;
        foreach (var unlock in unlocks.Entries)
        {
            if (unlock.Status == UnlockStatus.Available && unlock.GiverTerritory == here && unlock.GiverName is { Length: > 0 } name)
            {
                giverNames.Add(name);
            }
        }

        fallbackObjectIds.Clear();
        if (huntNameIds.Count == 0 && giverNames.Count == 0)
        {
            foreach (var id in NearestBattleNpcIds(3))
            {
                fallbackObjectIds.Add(id);
            }
        }

        Summary = $"{huntNameIds.Count} hunting target name(s), {giverNames.Count} quest giver(s), {fallbackObjectIds.Count} nearest-mob fallback(s)";
    }

    /// <summary>Fallback so the experiment is still decisive when no hunting log is active and no
    /// unlock giver is in the zone — mark whatever is closest and see if the icon renders at all.</summary>
    private IEnumerable<ulong> NearestBattleNpcIds(int count)
    {
        var player = objects.LocalPlayer;
        if (player is null)
        {
            return [];
        }

        return
        [
            .. objects
            .OfType<IBattleNpc>()
            .Where(npc => npc is { IsDead: false, IsTargetable: true })
            .OrderBy(npc => NavMath.Distance(
                npc.Position.X - player.Position.X,
                npc.Position.Y - player.Position.Y,
                npc.Position.Z - player.Position.Z))
            .Take(count)
            .Select(npc => npc.GameObjectId),
        ];
    }
}
