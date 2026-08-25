namespace Wayfarer;

/// <summary>What a game icon id turned out to be when it was asked about.
///
/// <para><b>Why three states and not two.</b> Dalamud's texture cache answers "is this loaded?", not
/// "does this exist?" — <c>ISharedImmediateTexture.TryGetWrap</c> returns false both for an id that
/// is not in the game files and for one that simply has not finished loading yet. Collapsing those
/// two into one boolean is what made the Hunting Log's creature portraits disappear: they are art
/// nothing else in the process touches, so every one of them was cold on the single frame it was
/// asked about, every answer was "false", and "false" was cached for the session.</para></summary>
internal enum GameIconAvailability
{
    /// <summary>The id resolves and its texture is loaded. Safe to draw, safe to remember.</summary>
    Present,

    /// <summary>The id resolves to a real game path but the texture is still loading. <b>Not an
    /// answer</b> — it must not be remembered, and it must not be read as absence.</summary>
    Pending,

    /// <summary>The id does not resolve to a game path at all, or its load failed outright. This is
    /// the real "a patch renumbered or removed it" signal, and the only one worth caching.</summary>
    Absent,
}
