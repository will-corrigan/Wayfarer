using System.Numerics;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;
using Wayfarer.Guidance;
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

    /// <summary>What the readout said about elevation last frame, which is what supplies the
    /// hysteresis in <see cref="Core.Ui.Elevation.Classify"/> — see there for why a single
    /// threshold is not enough.</summary>
    private ElevationHint lastElevation;

    /// <summary>What the readout said about a search-area objective last frame, which is what
    /// supplies the hysteresis in <see cref="Core.Ui.SearchArea.Classify"/> — the same reasoning as
    /// <see cref="lastElevation"/>, for the same reason: a boundary crossed on foot flickers without
    /// it.</summary>
    private SearchAreaHint lastSearchArea = SearchAreaHint.Outside;

    /// <summary>The last target position a ground height was resolved for, and the answer. One
    /// collision raycast per changed target rather than one per frame: the target only moves when
    /// the objective changes or a live-tracked mob walks.</summary>
    private Vector3? groundedFor;
    private float? groundedHeight;

    /// <summary>The guidance snapshot's source, for the surfaces that need to act on it rather than
    /// only read it — the readout's teleport, for one. Read-only by construction: this is
    /// the same <see cref="INavigationProvider"/> every other consumer already has.</summary>
    public INavigationProvider Navigator => navigator;

    /// <summary>Builds this frame's content. The words are the same on every surface: what differs
    /// between the readout's own addon and the fallback overlay is whether the line can be pressed,
    /// which each host decides for itself from the line's own <see cref="ReadoutLineAction"/> mark.
    /// </summary>
    public ReadoutContent Compose()
    {
        var state = navigator.Current;
        var distance = Distance(state);
        return ReadoutComposer.Compose(new ReadoutInputs
        {
            State = state,
            DistanceYalms = distance,
            HuntingSummary = HuntingSummary(),
            HuntingIsPrimary = string.Equals(state.SourceId, HuntingSourceId, StringComparison.Ordinal),
            NearbyUnlocks = NearbyUnlocks(),
            Elevation = TargetElevation(state),
            AreaHint = AreaHint(state, distance),
        });
    }

    /// <summary>Whether the player is outside or inside a search-area objective's circle right now
    /// — the distance passed in is the same one already computed for the arrow/distance line, so
    /// this measures against exactly the point the readout is otherwise treating as the target's
    /// position (the circle's centre).</summary>
    public SearchAreaHint AreaHint(NavigationState state, float? distanceYalms)
    {
        ArgumentNullException.ThrowIfNull(state);

        lastSearchArea = Core.Ui.SearchArea.Classify(distanceYalms, state.TargetRadiusYalms, lastSearchArea);
        return lastSearchArea;
    }

    /// <summary>Whether the arrow's target is meaningfully above or below the player right now.
    ///
    /// <para><b>The honest part.</b> The only height available for a target is the coordinate the
    /// objective came with, and for a curated hunting or unlock coordinate that height may never
    /// have been checked against the terrain. So it is checked here, against the game's own
    /// collision (<see cref="GroundHeight"/>), and when the check finds nothing the readout says
    /// nothing — no chevron, no words. A live-tracked target is exempt because its height is not a
    /// stored coordinate at all: it is where the object actually is, read from the object table this
    /// tick.</para>
    ///
    /// <para>Only same-zone targets are considered. In the cross-zone case the arrow points at an
    /// entrance or a shard whose height the router does not carry, and "above you" about a door in
    /// another territory would be meaningless even if it were true.</para></summary>
    public ElevationHint TargetElevation(NavigationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (objects.LocalPlayer is not { } player
            || !string.Equals(state.Mode, NavigationState.Modes.SameZone, StringComparison.Ordinal)
            || state.TargetX is not { } tx
            || state.TargetY is not { } ty
            || state.TargetZ is not { } tz)
        {
            lastElevation = ElevationHint.Level;
            return lastElevation;
        }

        var target = new Vector3(tx, ty, tz);
        var height = state.IsLiveTarget ? ty : Grounded(target);
        var delta = height is { } y ? y - player.Position.Y : (float?)null;

        lastElevation = Core.Ui.Elevation.Classify(delta, lastElevation);
        return lastElevation;
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
            Step = NextStep(state),
            StepTarget = StepTarget(state),
            DistanceYalms = Distance(state),
            HuntingIsPrimary = huntingIsPrimary,
            HuntingLabel = huntingIsPrimary ? DtrHuntingLabel() : null,

            // The same call the readout's own content is built from, deliberately: the info bar
            // must not be able to alert about a pickup the readout has been told not to mention.
            NearbyUnlockCount = NearbyUnlocks().Count,
        });
    }

    /// <summary>What the player actually has to do next, read off the same snapshot
    /// <see cref="ReadoutComposer"/> builds its lines from, and in the same order of precedence:
    /// a teleport is advised "first", so it beats everything; an aethernet hop is next; otherwise
    /// there is a coordinate to walk to, or there is nothing.
    ///
    /// <para>An unattuned aetheryte is deliberately not a teleport step. The readout says "you are
    /// not attuned there" in that case and the player's next move is to walk, so the bar must not
    /// put a crystal on it.</para></summary>
    private static DtrNextStep NextStep(NavigationState state)
    {
        if (!state.Engaged)
        {
            return DtrNextStep.None;
        }

        if (state.AetheryteName is { Length: > 0 } && state.AetheryteUnlocked)
        {
            return DtrNextStep.Teleport;
        }

        if (state.AethernetExitName is { Length: > 0 })
        {
            return DtrNextStep.Aethernet;
        }

        var walkable = string.Equals(state.Mode, NavigationState.Modes.SameZone, StringComparison.Ordinal)
            ? state.TargetX is not null && state.TargetZ is not null
            : state.EntranceX is not null && state.EntranceZ is not null;

        return walkable ? DtrNextStep.Walk : DtrNextStep.None;
    }

    private static string? StepTarget(NavigationState state) => NextStep(state) switch
    {
        DtrNextStep.Teleport => state.AetheryteName,
        DtrNextStep.Aethernet => state.AethernetExitName,
        _ => null,
    };

    private float? Grounded(Vector3 target)
    {
        if (groundedFor != target)
        {
            groundedFor = target;
            groundedHeight = GroundHeight.Resolve(target);
        }

        return groundedHeight;
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
