namespace Wayfarer.Core.Presentation;

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

    /// <summary>Open the Duty Finder at this duty: the target is inside it.</summary>
    /// <param name="DutyId">The duty's content finder condition id.</param>
    public sealed record OpenDuty(uint DutyId) : RoutePress;
}
