using System.Runtime.InteropServices;

namespace Wayfarer.Modules.Quests;

/// <summary>The instanced content a quest sends the player into. Both halves come off one Duty
/// Finder row and are only ever right together, so they travel together: handing them round as two
/// numbers is an invitation to pair a duty with another duty's territory.</summary>
/// <param name="Finder">The Duty Finder entry to queue for.</param>
/// <param name="Territory">The territory the instance runs in, when the row names one. It is the
/// instance's own and nowhere a player can walk to, so a step the data puts there is a step that
/// happens inside the duty.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct QuestDuty(uint Finder, uint? Territory);
