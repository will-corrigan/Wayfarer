namespace Wayfarer.Core.Ui;

/// <summary>How the plugin's own colour vocabulary maps onto the game's. Three names rather than
/// <c>Vector4</c>s so <see cref="UnlockStatusDisplay"/> stays in Core, where it can be asserted,
/// and the live <c>UIColor</c> lookup stays at the node, where it belongs.</summary>
public enum UnlockStatusTone
{
    /// <summary>An actionable row: the Duty Finder's own warm-cream list text.</summary>
    Normal,

    /// <summary>Locked, complete, or unverifiable — present, but not what you are looking at.</summary>
    Dimmed,

    /// <summary>Genuinely bad, which here means exactly one thing: permanently missed.</summary>
    Bad,
}
