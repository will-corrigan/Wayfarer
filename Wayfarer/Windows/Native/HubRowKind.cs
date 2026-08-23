namespace Wayfarer.Windows.Native;

/// <summary>What a hub list row is. Deliberately flat and uniform-height: KamiToolKit's
/// <c>ListNode&lt;T, TU&gt;</c> virtualizes on a single per-type row height, and a virtual list is
/// the only list shape in the toolkit that carries controller navigation and scroll-follows-focus
/// (<c>ScrollingNode&lt;VerticalListNode&gt;</c> has neither and cannot be given them). Section
/// headings are therefore rows too, exactly as they are in the game's own lists.</summary>
internal enum HubRowKind
{
    /// <summary>A gold section heading — "Central Thanalan (4)", "Modules".</summary>
    Heading,

    /// <summary>A selectable row that does something when confirmed.</summary>
    Entry,

    /// <summary>A plain informational line: an empty-list message, an unverified entry.</summary>
    Note,
}
