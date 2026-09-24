namespace Wayfarer.Guidance;

/// <summary>The one thing between modules and surfaces. Modules claim it to guide; surfaces read
/// what it publishes. It never writes words and never draws.
///
/// <para>Claiming, offering, resuming and yielding are game thread only: the frame loop reads who
/// holds guidance on that thread, and who holds it is remembered for the character playing.</para></summary>
internal interface IGuidance
{
    /// <summary>Raised after <see cref="Current"/> changes.</summary>
    event EventHandler<GuidanceChangedEventArgs> OnChanged;

    /// <summary>The source guiding right now, or null.</summary>
    IObjectiveSource? Holder { get; }

    /// <summary>What is being guided to, after routing, or null. Changes only when the words or
    /// the shape of the route change; see <see cref="GuidanceChange"/>.</summary>
    PublishedGuidance? Current { get; }

    /// <summary>Take focus now: what the player just chose to follow. Last one wins. The previous
    /// holder, if any, is told it was displaced before this source is first read, and waits to
    /// have focus back when this source yields.</summary>
    void Claim(IObjectiveSource source);

    /// <summary>Hold focus if nobody does, and otherwise wait for it without pushing anyone aside.
    /// For a module that always has something to say, such as the quest being followed, which
    /// offers itself whenever its settings are applied and must not take guidance from something
    /// the player chose.</summary>
    void Offer(IObjectiveSource source);

    /// <summary>Take focus back when this source is the one the character playing last had
    /// guidance from, and otherwise offer it. For a module coming up or a character logging in, so
    /// each character picks up where they left off rather than where the last one to play did.</summary>
    void Resume(IObjectiveSource source);

    /// <summary>Give focus up, or stop waiting for it. When the holder gives it up, whichever
    /// source it pushed aside most recently has focus back.</summary>
    void Yield(IObjectiveSource source);
}
