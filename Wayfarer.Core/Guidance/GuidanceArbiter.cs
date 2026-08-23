namespace Wayfarer.Core.Guidance;

/// <summary>Decides which single source owns the arrow, and nothing else. Routing, suppression,
/// projection, chain progression and every side effect live elsewhere, deliberately — a mediator
/// that accretes domain logic is how you get a 1300-line controller.
///
/// The arbiter is PAYLOAD-BLIND: it never reads a <see cref="GuidanceObjective"/>'s quest id, kill
/// count, coordinates or territory. It cannot re-derive completion, because there is no code path
/// that looks. That is what makes the "objective vanishes one tick after selection" class of bug
/// structurally impossible rather than merely fixed.
///
/// Precedence, in this exact order:
/// <list type="number">
/// <item>Engaged beats ambient, always. If a source holds the token, poll it FIRST; a non-null
/// offer is the active objective and no ambient source is polled at all.</item>
/// <item>The token auto-releases on null — and falls through to step 3 IN THE SAME TICK, so there
/// is never a one-frame flash of the ambient objective between the last leg of a route and
/// idle.</item>
/// <item>Ambient sources resolve in registration order; first non-null wins.</item>
/// <item>Nothing at all — null.</item>
/// </list>
///
/// Two things that cannot happen, by type rather than by discipline: engagement is a single token
/// owned here (not a bool per source), so "two explicit modes at once" is unrepresentable; and no
/// source can seize the token spontaneously mid-route, because the only way to take it is a
/// deliberate user action that releases the incumbent first.</summary>
/// <param name="logError">Optional sink for a guarded source failure, called at most once per
/// source id. Injected rather than referenced so this class stays free of any logging framework —
/// the same idiom every other pure decision in Wayfarer.Core uses.</param>
public sealed class GuidanceArbiter(Action<string, Exception>? logError = null) : IGuidanceArbiter
{
    private readonly List<IGuidanceSource> sources = [];
    private readonly HashSet<string> loggedFailures = new(StringComparer.Ordinal);

    private IGuidanceSource? currentOwner;

    public event Action<GuidanceObjective?>? OnObjectiveChanged;

    public IGuidanceSource? EngagedSource { get; private set; }

    public GuidanceObjective? Current { get; private set; }

    public GuidanceEngagement Engagement { get; private set; }

    public void Register(IGuidanceSource source)
    {
        if (!sources.Contains(source))
        {
            sources.Add(source);
        }
    }

    public void Unregister(IGuidanceSource source)
    {
        sources.Remove(source);
        if (ReferenceEquals(EngagedSource, source))
        {
            ReleaseToken(DisengageReason.ModuleDisabled);
        }

        if (ReferenceEquals(currentOwner, source))
        {
            Publish(null, null);
        }
    }

    public void Engage(IGuidanceSource source)
    {
        Register(source);
        if (ReferenceEquals(EngagedSource, source))
        {
            return; // already ours — re-selecting within the same source must not disengage it
        }

        ReleaseToken(DisengageReason.Preempted);
        EngagedSource = source;
    }

    public void Release(IGuidanceSource source)
    {
        if (ReferenceEquals(EngagedSource, source))
        {
            ReleaseToken(DisengageReason.UserCancelled);
        }
    }

    public void ReleaseAll() => ReleaseToken(DisengageReason.UserCancelled);

    public GuidanceObjective? Tick(GuidanceContext ctx)
    {
        IGuidanceSource? justReleased = null;
        if (EngagedSource is { } holder)
        {
            if (SafePoll(holder, ctx) is { } offer)
            {
                Publish(holder, offer);
                return Current;
            }

            justReleased = holder;
            ReleaseToken(DisengageReason.Completed);
        }

        foreach (var source in sources)
        {
            if (ReferenceEquals(source, justReleased))
            {
                continue; // already polled this tick and it had nothing
            }

            if (SafePoll(source, ctx) is not { } offer)
            {
                continue;
            }

            // A source that does not hold the token is ambient no matter what it claims: the token
            // is the only thing that confers engagement.
            Publish(source, offer with { Engagement = GuidanceEngagement.Ambient });
            return Current;
        }

        Publish(null, null);
        return null;
    }

    private static bool SameIdentity(
        GuidanceObjective? a, GuidanceEngagement aEngagement, GuidanceObjective? b, GuidanceEngagement bEngagement)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.Key == b.Key && aEngagement == bEngagement;
    }

    private GuidanceOffer? SafePoll(IGuidanceSource source, GuidanceContext ctx)
    {
        try
        {
            return source.Poll(ctx);
        }
        catch (Exception ex)
        {
            LogOnce(source.SourceId, "poll failed", ex);
            if (ReferenceEquals(EngagedSource, source))
            {
                // A source that throws is unavailable, not merely idle: it forfeits the token and
                // drops its plan rather than being re-polled into the same exception every frame.
                ReleaseToken(DisengageReason.ModuleDisabled);
            }

            return null;
        }
    }

    private void ReleaseToken(DisengageReason reason)
    {
        if (EngagedSource is not { } source)
        {
            return;
        }

        EngagedSource = null;
        try
        {
            source.OnDisengaged(reason);
        }
        catch (Exception ex)
        {
            LogOnce(source.SourceId, "disengage failed", ex);
        }
    }

    private void Publish(IGuidanceSource? owner, GuidanceOffer? offer)
    {
        var objective = offer?.Objective;
        var engagement = offer?.Engagement ?? GuidanceEngagement.Ambient;
        if (objective is not null
            && engagement == GuidanceEngagement.Engaged
            && string.IsNullOrEmpty(objective.Copy.SourceLabel))
        {
            throw new InvalidOperationException(
                $"Engaged objective '{objective.Key}' has no SourceLabel: an explicit mode must always name itself, "
                + "because the readout is the only mode indicator the player has.");
        }

        var previous = Current;
        var previousOwner = currentOwner;
        var changed = !SameIdentity(previous, Engagement, objective, engagement);

        Current = objective;
        Engagement = engagement;
        currentOwner = owner;

        if (!changed)
        {
            return; // same objective, fresher payload — no event, so no side effect re-fires
        }

        if (previous is not null && previousOwner is not null)
        {
            SafeNotify(previousOwner, s => s.OnObjectiveDeactivated(previous), "deactivation");
        }

        if (objective is not null && owner is not null)
        {
            SafeNotify(owner, s => s.OnObjectiveActivated(objective), "activation");
        }

        OnObjectiveChanged?.Invoke(objective);
    }

    private void SafeNotify(IGuidanceSource source, Action<IGuidanceSource> notify, string what)
    {
        try
        {
            notify(source);
        }
        catch (Exception ex)
        {
            LogOnce(source.SourceId, what, ex);
        }
    }

    private void LogOnce(string sourceId, string what, Exception ex)
    {
        if (loggedFailures.Add($"{sourceId}:{what}"))
        {
            logError?.Invoke(
                $"Wayfarer guidance: the '{sourceId}' source threw on {what}, so it may not have noticed that "
                + "the objective changed and its part of the readout can be stale until something else "
                + "changes it. Every other source is unaffected. Reported once per source.",
                ex);
        }
    }
}
