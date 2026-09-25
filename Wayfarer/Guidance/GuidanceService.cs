using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
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

    /// <summary>How long a frame of guidance has to take before it is worth saying so. The game
    /// draws at sixty a second, so anything near this has already been seen as a stutter.</summary>
    private static readonly TimeSpan SlowFrame = TimeSpan.FromMilliseconds(20);

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly RouteGraph graph;
    private readonly HolderMemory memory;
    private readonly Flight flight;
    private readonly IPluginLog log;

    /// <summary>Who holds guidance and who waits to have it back.</summary>
    private readonly Focus focus = new();

    private bool broken;
    private ObjectiveEntry? routedTo;
    private Place? routedFrom;
    private bool routedAirborne;
    private Route? route;

    public GuidanceService(
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objects,
        RouteGraph graph,
        HolderMemory memory,
        Flight flight,
        IPluginLog log)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.condition = condition;
        this.objects = objects;
        this.graph = graph;
        this.memory = memory;
        this.flight = flight;
        this.log = log;
        framework.Update += OnUpdate;
    }

    /// <inheritdoc/>
    public event EventHandler<GuidanceChangedEventArgs>? OnChanged;

    /// <inheritdoc/>
    public IObjectiveSource? Holder => focus.Holder;

    /// <inheritdoc/>
    public PublishedGuidance? Current { get; private set; }

    /// <inheritdoc/>
    public void Claim(IObjectiveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Remembered(() => focus.Claim(source)?.Displaced());
    }

    /// <inheritdoc/>
    public void Offer(IObjectiveSource source) => Remembered(() => focus.Offer(source));

    /// <inheritdoc/>
    public void Resume(IObjectiveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Remembered(() => focus.Resume(source, memory.Last)?.Displaced());
    }

    /// <inheritdoc/>
    public void Yield(IObjectiveSource source) => Remembered(() => focus.Yield(source));

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

    /// <summary>Changes who holds focus, and notes the new holder for the character playing when it
    /// changed. Only a change is noted: a character just logged in still has the last one's holder
    /// until a source resumes or lets go, and that holder was not this character's choice.</summary>
    private void Remembered(Action change)
    {
        var before = focus.Holder;
        change();
        if (!ReferenceEquals(before, focus.Holder))
        {
            memory.Remember(focus.Holder);
        }
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
        var started = Stopwatch.GetTimestamp();
        if (Holder is not { } source || source.Current is not { } objective)
        {
            return null;
        }

        var asked = Stopwatch.GetTimestamp();
        var target = objective.Guided(Standing());
        var way = RouteTo(target);
        Slow(started, asked, Stopwatch.GetTimestamp());

        return new PublishedGuidance(source, objective, target, way);
    }

    /// <summary>Notes where a slow frame went, for whoever is working on this. A frame of
    /// guidance is two things — asking whoever holds it what it is about, and working out the way
    /// there — and which of them cost the frame cannot be told from outside. Nothing a player can
    /// do anything about, so it is said quietly and only when a frame was slow.</summary>
    private void Slow(long started, long asked, long done)
    {
        var whole = Stopwatch.GetElapsedTime(started, done);
        if (whole < SlowFrame)
        {
            return;
        }

        log.Debug(
            $"a frame of guidance took {whole.TotalMilliseconds:F0}ms: " +
            $"{Stopwatch.GetElapsedTime(started, asked).TotalMilliseconds:F0}ms asking {Holder?.Name ?? "nobody"} what it is about, " +
            $"{Stopwatch.GetElapsedTime(asked, done).TotalMilliseconds:F0}ms working out the way there.");
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

        // Taking off or landing changes what height costs, so the way there is worked out again.
        var airborne = condition[ConditionFlag.InFlight] || condition[ConditionFlag.Diving];
        if (ReferenceEquals(target, routedTo) && routedFrom is { } last && Settled(last, from) && airborne == routedAirborne)
        {
            return route;
        }

        routedTo = target;
        routedFrom = from;
        routedAirborne = airborne;
        return route = graph.FindRoute(from, ends, PlayerState.IsAttuned, PlayerState.IsQuestComplete, PlayerState.IsFestivalOn, airborne, flight.CanFly);
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
