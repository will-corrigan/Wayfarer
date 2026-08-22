namespace Wayfarer.Core.Guidance;

/// <summary>The single writer for "what is the arrow pointing at". Sources call
/// <see cref="Engage"/> from their own typed selection APIs; presentations call
/// <see cref="ReleaseAll"/> and nothing else.</summary>
public interface IGuidanceArbiter
{
    /// <summary>Raised when the active objective's IDENTITY changes — a different
    /// <see cref="ObjectiveKey"/>, a change between engaged and ambient, or a transition to/from
    /// nothing. NOT raised for position or progress updates within the same key: a live-tracked mob
    /// re-emitting its position every frame must not re-fire this, or every subscriber with a side
    /// effect (the map flag above all) would run at frame rate. Carries the new active objective,
    /// or null for idle.</summary>
    event Action<GuidanceObjective?>? OnObjectiveChanged;

    /// <summary>The source currently holding the engagement token, or null when guidance is
    /// ambient or idle.</summary>
    IGuidanceSource? EngagedSource { get; }

    /// <summary>The active objective as of the last <see cref="Tick"/>.</summary>
    GuidanceObjective? Current { get; }

    /// <summary>Whether <see cref="Current"/> is an explicit mode or the ambient default.</summary>
    GuidanceEngagement Engagement { get; }

    void Register(IGuidanceSource source);

    /// <summary>Removes a source (its module was disabled or unloaded), releasing the token first
    /// if it held it.</summary>
    void Unregister(IGuidanceSource source);

    /// <summary>Hands the token to <paramref name="source"/>, releasing the incumbent as its first
    /// step. Called BY THE SOURCE from its own typed selection API, never by a presentation — so
    /// the "select the target, then engage" ordering has exactly one call site per user action and
    /// cannot be got wrong.</summary>
    void Engage(IGuidanceSource source);

    /// <summary>Releases the token if <paramref name="source"/> holds it; a no-op otherwise.</summary>
    void Release(IGuidanceSource source);

    /// <summary>The universal exit, offered by every presentation.</summary>
    void ReleaseAll();

    /// <summary>Resolves the active objective for this frame. Framework thread only.</summary>
    GuidanceObjective? Tick(GuidanceContext ctx);
}
