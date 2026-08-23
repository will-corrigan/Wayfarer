using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;

namespace Wayfarer.Guidance;

/// <summary>The per-frame guidance loop, in four steps and no decisions of its own: check the
/// display gates, ask the arbiter which objective is active, ask the router how to get there,
/// project the answer into the one published <see cref="NavigationState"/>.
///
/// The published state is a volatile reference swap of an immutable object, so the widget and the
/// IPC gate can read it from any thread while this runs on the framework thread — the same model
/// the navigator used before, kept deliberately.</summary>
internal sealed class GuidanceService(
    IPluginLog log,
    QuestHelperConfig cfg,
    IClientState clientState,
    ICondition condition,
    IObjectTable objects,
    GuidanceArbiter arbiter,
    GuidanceRouter router)
{
    private volatile NavigationState current = new();
    private bool errorLogged;

    public NavigationState Current => current;

    public GuidanceArbiter Arbiter => arbiter;

    public void OnUpdate(IFramework framework)
    {
        try
        {
            current = Compute();
            errorLogged = false;
        }
        catch (Exception ex)
        {
            if (!errorLogged)
            {
                const string message =
                    "Wayfarer guidance: working out where to go failed, so the readout will say it has no "
                    + "location data until it recovers. Reported once per run of failures, not once per tick.";
                log.Error(ex, message);
                errorLogged = true;
            }

            current = new() { Mode = NavigationState.Modes.NoLocation, Reason = "no location data" };
        }
    }

    private NavigationState Compute()
    {
        var player = objects.LocalPlayer;
        var inCutscene = condition[ConditionFlag.OccupiedInCutSceneEvent]
            || condition[ConditionFlag.WatchingCutscene]
            || condition[ConditionFlag.WatchingCutscene78];
        var suppression = new SuppressionInputs(
            LoggedIn: clientState.IsLoggedIn,
            PlayerPresent: player != null,
            InCutscene: inCutscene,
            BetweenAreas: condition[ConditionFlag.BetweenAreas],
            InCombat: condition[ConditionFlag.InCombat],
            HideInCombat: cfg.ArrowHideInCombat,
            BoundByDuty: condition[ConditionFlag.BoundByDuty],
            HideInDuty: cfg.ArrowHideInDuty);

        // Hidden, NOT released: the engagement token survives a cutscene, a duty or a fight, so the
        // same objective — same chain position — is still there when the readout comes back.
        if (GuidanceSuppression.ShouldHide(suppression) || player is null)
        {
            return new();
        }

        var pos = player.Position;
        var ctx = new GuidanceContext(
            clientState.TerritoryType, clientState.MapId, pos.X, pos.Y, pos.Z, LoggedIn: true);

        if (arbiter.Tick(ctx) is not { } objective)
        {
            return new() { Mode = NavigationState.Modes.Idle };
        }

        return GuidanceProjection.Build(objective, arbiter.Engagement, router.Route(objective, ctx));
    }
}
