using Dalamud.Plugin.Services;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

/// <summary>Gathers what the guidance readout should say, once, for whichever surface is drawing
/// it. The native overlay and the ImGui fallback both read this, so the two can never disagree
/// about what is on screen — before this existed the widget's layout logic was the only definition
/// of the readout, and anything that replaced it would have had to reproduce it by hand.</summary>
internal sealed class ReadoutFeed(
    INavigationProvider navigator,
    ModuleRegistry modules,
    QuestHelperConfig cfg,
    IObjectTable objects)
{
    private const string HuntingSourceId = "hunting";

    /// <summary>The guidance snapshot's source, for the surfaces that need to act on it rather than
    /// only read it — the clickable readout's teleport, for one. Read-only by construction: this is
    /// the same <see cref="INavigationProvider"/> every other consumer already has.</summary>
    public INavigationProvider Navigator => navigator;

    /// <summary>Builds this frame's content. <paramref name="teleportOnClick"/> is true only where
    /// the surface can actually be clicked — the overlay is click-through by construction, so it
    /// never promises otherwise.</summary>
    public ReadoutContent Compose(bool teleportOnClick)
    {
        var state = navigator.Current;
        return ReadoutComposer.Compose(new ReadoutInputs
        {
            State = state,
            DistanceYalms = Distance(state),
            HuntingSummary = HuntingSummary(),
            HuntingIsPrimary = string.Equals(state.SourceId, HuntingSourceId, StringComparison.Ordinal),
            NearbyUnlocks = NearbyUnlocks(),
            TeleportOnClick = teleportOnClick,
        });
    }

    /// <summary>Distance to whatever the arrow is pointing at, against the player's live position.</summary>
    public float? Distance(NavigationState state)
    {
        var player = objects.LocalPlayer;
        if (player is null)
        {
            return null;
        }

        var (x, z) = state.Mode switch
        {
            NavigationState.Modes.SameZone => (state.TargetX, state.TargetZ),
            NavigationState.Modes.OtherZone => (state.EntranceX, state.EntranceZ),
            _ => (null, null),
        };

        if (x is not { } tx || z is not { } tz)
        {
            return null;
        }

        var ty = string.Equals(state.Mode, NavigationState.Modes.SameZone, StringComparison.Ordinal)
            ? state.TargetY
            : null;

        return NavMath.Distance(
            tx - player.Position.X, (ty ?? player.Position.Y) - player.Position.Y, tz - player.Position.Z);
    }

    /// <summary>Whether the readout should be on screen at all.</summary>
    public bool ShouldShow() =>
        !cfg.WidgetHidden
        && !string.Equals(navigator.Current.Mode, NavigationState.Modes.Hidden, StringComparison.Ordinal);

    /// <summary>Builds this frame's server info bar text. Unlike <see cref="Compose"/>, this is
    /// never gated on <see cref="ShouldShow"/> — the bar entry is the plugin's one always-visible
    /// surface precisely because the readout itself can be hidden or click-through.</summary>
    public DtrText ComposeDtr()
    {
        var state = navigator.Current;
        var huntingIsPrimary = string.Equals(state.SourceId, HuntingSourceId, StringComparison.Ordinal);
        return DtrComposer.Compose(new DtrInputs
        {
            Engaged = state.Engaged,
            RouteStop = state.RouteStop,
            RouteTotal = state.RouteTotal,
            HuntingIsPrimary = huntingIsPrimary,
            HuntingLabel = huntingIsPrimary ? DtrHuntingLabel() : null,
            NearbyUnlockCount = NearbyUnlocks().Count,
        });
    }

    // "Rank 2 4/5" — the hunting rank the game itself reports (see HuntingWindow's identical
    // "rank {N}" wording) alongside the current target's kill count, already short enough for the
    // bar. Rank is occasionally unknown (e.g. between log reads) without the target itself being
    // gone, so it degrades to the kill count alone rather than disappearing.
    private string? DtrHuntingLabel()
    {
        if (modules.Get<HuntingLogModule>() is not { Enabled: true } hunting
            || hunting.Hunting.CurrentTarget is not { } target)
        {
            return null;
        }

        return hunting.Hunting.CurrentRank is { } rank
            ? $"Rank {rank} {target.Killed}/{target.Required}"
            : $"{target.Killed}/{target.Required}";
    }

    private string? HuntingSummary()
    {
        if (modules.Get<HuntingLogModule>() is not { Enabled: true } hunting
            || !hunting.Config.ShowOnWidget
            || hunting.Hunting.CurrentTarget is not { } target)
        {
            return null;
        }

        return $"Hunting: {target.MonsterName} {target.Killed}/{target.Required}";
    }

    /// <summary>The unlocks available in this zone right now, with a live distance to each —
    /// restored from the pre-rewrite widget, where this was the thing that made opening the
    /// checklist optional.
    ///
    /// Read straight off <see cref="UnlockService.GlanceableHere"/>, which the module already keeps
    /// to the nearest few and recomputes only on a zone or level change. Nothing here rescans; only
    /// the distance is recomputed, and that is the same arithmetic the arrow already pays for.
    /// These are display lines and nothing more — they carry no direction and cannot become the
    /// active objective, which is what keeps the one-active-objective rule intact.</summary>
    private List<string> NearbyUnlocks()
    {
        if (modules.Get<UnlockChecklistModule>() is not { Enabled: true } unlockModule
            || !unlockModule.Config.ShowOnWidget)
        {
            return [];
        }

        var here = unlockModule.Unlocks.GlanceableHere;
        if (here.Count == 0)
        {
            return [];
        }

        var player = objects.LocalPlayer;
        var names = new List<string>(here.Count);
        foreach (var unlock in here)
        {
            if (player is null)
            {
                names.Add(unlock.Def.Unlock);
                continue;
            }

            var distance = NavMath.Distance(
                unlock.GiverX - player.Position.X,
                unlock.GiverY - player.Position.Y,
                unlock.GiverZ - player.Position.Z);
            names.Add($"{unlock.Def.Unlock} ({NavMath.FormatDistance(distance)})");
        }

        return names;
    }
}
