using System.Runtime.InteropServices;

namespace Wayfarer.Modules.Quests;

/// <summary>A quest waiting behind a Duty Finder row: which one it is, and the mark the game draws
/// for it. The two travel together because a mark with no quest behind it is nothing to press, and
/// a quest with no mark is nothing to draw.</summary>
/// <param name="QuestId">The quest, so pressing the mark opens its page in the journal.</param>
/// <param name="Icon">The mark the game itself draws for that quest.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct QuestBehind(ushort QuestId, uint Icon);
