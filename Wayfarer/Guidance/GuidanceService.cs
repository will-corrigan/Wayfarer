using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Holds focus, runs the frame loop, publishes. Every frame while a source holds focus:
/// read its objective, route to its first reachable entry from where the player stands, and if the
/// words or the shape of the route differ from last frame, publish. Nothing else in the app has a
/// frame loop; modules are read by this one and surfaces are told by it.</summary>
internal sealed unsafe class GuidanceService : IGuidance, IDisposable
{
    /// <summary>How far the player has to move before the way there is worked out again. Short
    /// enough that a different aetheryte or door cannot quietly become the nearer one, long enough
    /// that walking does not start a search every frame.</summary>
    private const float RouteRethinkYalms = 10f;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly RouteGraph graph;
    private readonly IInteractions interactions;
    private readonly IPluginLog log;

    private bool broken;
    private ObjectiveEntry? routedTo;
    private string? guiding;
    private Place? routedFrom;
    private Route? route;

    public GuidanceService(
        IFramework framework,
        IClientState clientState,
        IObjectTable objects,
        RouteGraph graph,
        IInteractions interactions,
        IPluginLog log)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.objects = objects;
        this.graph = graph;
        this.interactions = interactions;
        this.log = log;
        framework.Update += OnUpdate;
    }

    /// <inheritdoc/>
    public event EventHandler<GuidanceChangedEventArgs>? OnChanged;

    /// <inheritdoc/>
    public IObjectiveSource? Holder { get; private set; }

    /// <inheritdoc/>
    public PublishedGuidance? Current { get; private set; }

    /// <inheritdoc/>
    public void Claim(IObjectiveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(Holder, source))
        {
            return;
        }

        var displaced = Holder;
        Holder = source;
        displaced?.Displaced();
    }

    /// <inheritdoc/>
    public void Yield(IObjectiveSource source)
    {
        if (ReferenceEquals(Holder, source))
        {
            Holder = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => framework.Update -= OnUpdate;

    /// <summary>Everywhere a destination could end, for routing to choose between. A duty and a
    /// gate end nowhere on the map; a thing the module picked out ends where it stood.</summary>
    private static IReadOnlyList<Place> Ends(Destination where) => where switch
    {
        Destination.Reachable reachable => reachable.Places,
        Destination.AtObject thing => [thing.At],
        _ => [],
    };

    /// <summary>Whether the player has stayed put enough that the last search still answers: the
    /// same zone, and not far enough from where they were standing for a different aetheryte or
    /// door to have become the nearer one.</summary>
    private static bool Settled(Place last, Place now)
    {
        var (dx, dy, dz) = (last.X - now.X, last.Y - now.Y, last.Z - now.Z);
        return last.Territory == now.Territory
            && (dx * dx) + (dy * dy) + (dz * dz) < RouteRethinkYalms * RouteRethinkYalms;
    }

    private void OnUpdate(IFramework tick)
    {
        if (broken)
        {
            return;
        }

        try
        {
            Publish(Compute());
        }
        catch (Exception ex)
        {
            // The reason this threw will not have changed by the next frame, so guidance stops
            // rather than throwing sixty times a second behind a log line nobody sees again.
            broken = true;
            log.Error(ex, "computing guidance threw, so guidance is switched off for this session.");
            Publish(null);
        }
    }

    private PublishedGuidance? Compute()
    {
        if (Holder is not { } source || source.Current is not { } objective)
        {
            return null;
        }

        var target = objective.Guided();
        Rethink(target);
        return new PublishedGuidance(source, objective, target, RouteTo(target));
    }

    /// <summary>What the player has already tried belongs to the step they tried it for. When the
    /// guidance moves on to something else, the slate is wiped: the same thing standing in the next
    /// step's area is a thing they have not tried for that step.</summary>
    private void Rethink(ObjectiveEntry? target)
    {
        // Only a step of its own wipes the slate. Guidance can be without a target for a frame —
        // between zones, or while a route is thought about again — and that is not the player
        // moving on to something else.
        if (target?.Text is { } now && !string.Equals(now, guiding, StringComparison.Ordinal))
        {
            guiding = now;
            interactions.Forget();
        }
    }

    /// <summary>The way to the target, searched again only when the target changed or the player
    /// has moved far enough for the answer to differ. A search runs the whole graph and allocates
    /// as it goes, so running one every frame spends a tenth of the frame to be told the same
    /// thing sixty times.</summary>
    private Route? RouteTo(ObjectiveEntry? target)
    {
        if (target is null || Ends(target.Where) is not { Count: > 0 } ends || Standing() is not { } from)
        {
            routedTo = null;
            routedFrom = null;
            return route = null;
        }

        if (ReferenceEquals(target, routedTo) && routedFrom is { } last && Settled(last, from))
        {
            return route;
        }

        routedTo = target;
        routedFrom = from;
        return route = graph.FindRoute(from, ends, PlayerState.IsAttuned);
    }

    /// <summary>Where the player stands this frame, or null when there is no player to stand.</summary>
    private Place? Standing()
    {
        if (!clientState.IsLoggedIn || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var position = player.Position;
        return new Place(clientState.TerritoryType, clientState.MapId, position.X, position.Y, position.Z);
    }

    private void Publish(PublishedGuidance? now)
    {
        if (GuidanceChange.IsSame(Current, now))
        {
            return;
        }

        var previous = Current;
        Current = now;
        OnChanged?.Invoke(this, new GuidanceChangedEventArgs(previous, now));
    }
}
