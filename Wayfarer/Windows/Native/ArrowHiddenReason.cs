namespace Wayfarer.Windows.Native;

/// <summary>Why the readout is not drawing a direction arrow this frame.
///
/// This exists to be logged. An arrow that does not appear looks identical whatever the cause —
/// the guidance says not to show one, there is no target position, there is no player yet, or the
/// texture could not be drawn — and without this the only way to tell them apart was to read the
/// source and guess. <see cref="GuidanceOverlayNode"/> logs one line per change of reason, so the
/// log answers "why is there no arrow?" on its own.</summary>
internal enum ArrowHiddenReason
{
    /// <summary>Not hidden: the arrow is being drawn.</summary>
    None,

    /// <summary>The composed readout asked for no arrow — nothing active has a direction.</summary>
    NotRequested,

    /// <summary>The active objective carries no target coordinates to point at.</summary>
    NoTargetCoordinates,

    /// <summary>There is no local player to measure a bearing from.</summary>
    NoPlayer,

    /// <summary>The chevron texture is not loaded or not ready, so the words are shown instead.</summary>
    TextureUnavailable,
}
