using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Routing;

namespace Wayfarer.App.Guidance;

/// <summary>Holds focus, runs the frame loop, publishes. Every frame while a source holds focus:
/// read its objective, route to its first reachable entry from where the player stands, and if the
/// words or the shape of the route differ from last frame, publish. Nothing else in the app has a
/// frame loop; modules are read by this one and surfaces are told by it.</summary>
internal sealed unsafe class GuidanceService : IGuidance, IDisposable
{
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly RouteGraph graph;
    private readonly IPluginLog log;
    private bool loggedFailure;

    public GuidanceService(
        IFramework framework,
        IClientState clientState,
        IObjectTable objects,
        RouteGraph graph,
        IPluginLog log)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.objects = objects;
        this.graph = graph;
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

    private void OnUpdate(IFramework tick)
    {
        try
        {
            Publish(Compute());
        }
        catch (Exception ex)
        {
            // Once: this runs every frame, and the reason it would throw does not change between
            // frames. Guidance stops rather than logging sixty lines a second.
            if (!loggedFailure)
            {
                loggedFailure = true;
                log.Error(ex, "computing guidance threw, so guidance is switched off for this session.");
            }

            Publish(null);
        }
    }

    private PublishedGuidance? Compute()
    {
        if (Holder is not { } source || source.Current is not { } objective)
        {
            return null;
        }

        var target = objective.FirstReachable();
        var route = target?.Where is Destination.Reachable reachable && Standing() is { } from
            ? graph.FindRoute(from, reachable.Places, PlayerState.IsAttuned)
            : null;
        return new PublishedGuidance(source, objective, target, route);
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
