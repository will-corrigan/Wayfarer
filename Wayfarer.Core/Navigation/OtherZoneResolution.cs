namespace Wayfarer.Core.Navigation;

/// <summary>Which of the three ways to present an OtherZone objective applies, once
/// route costing has run. Pure decision extracted from QuestNavigator.OtherZone (the
/// <c>chosen == null ? fallbackWhenNoCandidate : route</c> branch) and ArrowWindow (the
/// interior-message text it used to reconstruct from raw state fields) so this
/// three-way choice — and the message text itself — has unit-test coverage
/// independent of Dalamud.</summary>
public enum OtherZoneOutcome
{
    /// <summary>Route costing found a real candidate (aethernet/entrance/teleport) —
    /// build the OtherZone state from it.</summary>
    Route,

    /// <summary>No candidate, but the caller supplied a marker-based fallback state (an
    /// exact live-marker position, from QuestNavigator's MarkerMatch.TerritoryOnly
    /// path) — return that fallback state as-is.</summary>
    MarkerFallback,

    /// <summary>Neither a route nor a fallback — the honest "we don't know how to get
    /// you there" message (<see cref="OtherZoneResolution.InteriorMessage"/>).</summary>
    InteriorMessage,
}

public static class OtherZoneResolution
{
    /// <summary>The literal reason text for <see cref="OtherZoneOutcome.InteriorMessage"/>
    /// — single source of truth so QuestNavigator sets it once on
    /// <see cref="NavigationState.Reason"/> and ArrowWindow just displays it, rather than
    /// each side independently deciding when to show it.</summary>
    public static string InteriorMessage(string? zoneName) =>
        $"In {zoneName ?? "another zone"} — find the entrance";

    /// <summary>The same guidance with the place name left out, for when the readout has already
    /// said it on a line above (<c>ReadoutComposer.AlreadySaid</c>). Kept beside the message it
    /// shortens so the two forms cannot drift apart. Any other <see cref="NavigationState.Reason"/>
    /// — from a duty, a missing location, or anywhere else — is returned untouched: this rewrites
    /// only the one sentence it owns.</summary>
    public static string WithoutZoneName(string message, string? zoneName) =>
        string.Equals(message, InteriorMessage(zoneName), StringComparison.Ordinal)
            ? "Find the entrance"
            : message;

    /// <summary>Picks the outcome: a real routed candidate always wins; failing that, a
    /// caller-supplied marker fallback wins (it's still an exact position, better than
    /// nothing even without a known route); only when neither exists does this resolve
    /// to the plain interior message.</summary>
    public static OtherZoneOutcome Resolve(RouteCandidate? chosen, NavigationState? fallbackWhenNoCandidate)
    {
        if (chosen != null)
        {
            return OtherZoneOutcome.Route;
        }

        return fallbackWhenNoCandidate != null ? OtherZoneOutcome.MarkerFallback : OtherZoneOutcome.InteriorMessage;
    }
}
