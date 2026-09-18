namespace Wayfarer.Core.Guidance;

/// <summary>What was being guided to before, and what is now. Either may be null: nothing to
/// something when a module claims focus, something to nothing when it yields.</summary>
public sealed class GuidanceChangedEventArgs(PublishedGuidance? previous, PublishedGuidance? now) : EventArgs
{
    /// <summary>The guidance before this change, or null.</summary>
    public PublishedGuidance? Previous { get; } = previous;

    /// <summary>The guidance now, or null.</summary>
    public PublishedGuidance? Now { get; } = now;
}
