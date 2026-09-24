namespace Wayfarer.Presentation;

/// <summary>What pressing a route line does. Decided from the guidance when the line is composed
/// and again at the moment of the press, so a stale line can never act on a stale route.</summary>
public abstract record RoutePress
{
    private RoutePress()
    {
    }

    /// <summary>Teleport to this aetheryte: the route's first leg.</summary>
    /// <param name="AetheryteId">The aetheryte's row id.</param>
    public sealed record Teleport(uint AetheryteId) : RoutePress;

    /// <summary>Something only the module that produced the guidance can do. The app performs
    /// nothing; the source is asked through <see cref="Wayfarer.Guidance.IObjectiveSource.PressRoute"/>.</summary>
    public sealed record Own : RoutePress;

    /// <summary>Open the Duty Finder at this duty: the target is inside it.</summary>
    /// <param name="DutyId">The duty's content finder condition id.</param>
    public sealed record OpenDuty(uint DutyId) : RoutePress;

    /// <summary>Open the Duty Finder at this roulette: the target is whichever duty it picks.</summary>
    /// <param name="RouletteId">The roulette's row in <c>ContentRoulette</c>.</param>
    public sealed record OpenRoulette(byte RouletteId) : RoutePress;
}
