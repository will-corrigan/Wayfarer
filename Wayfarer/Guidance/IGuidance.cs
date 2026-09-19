namespace Wayfarer.Guidance;

/// <summary>The one thing between modules and surfaces. Modules claim it to guide; surfaces read
/// what it publishes. It never writes words and never draws.</summary>
internal interface IGuidance
{
    /// <summary>Raised after <see cref="Current"/> changes.</summary>
    event EventHandler<GuidanceChangedEventArgs> OnChanged;

    /// <summary>The source guiding right now, or null.</summary>
    IObjectiveSource? Holder { get; }

    /// <summary>What is being guided to, after routing, or null. Changes only when the words or
    /// the shape of the route change; see <see cref="GuidanceChange"/>.</summary>
    PublishedGuidance? Current { get; }

    /// <summary>Take focus. Last one wins: the previous holder, if any, is told it was displaced
    /// before this source is first read.</summary>
    void Claim(IObjectiveSource source);

    /// <summary>Give focus up. Does nothing unless this source holds it.</summary>
    void Yield(IObjectiveSource source);
}
