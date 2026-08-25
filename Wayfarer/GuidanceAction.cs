namespace Wayfarer;

/// <summary>One thing the player can ask Wayfarer to do, as a label and the doing of it.
///
/// <para>Deliberately not a Dalamud menu item and not a KamiToolKit one: the same action is offered
/// in the game's own right-click menu (<see cref="ContextMenuActions"/>) and in the menu the readout
/// drops when a controller asks it for subcommands (<see cref="Windows.Native.ReadoutMenu"/>), and
/// those two APIs have nothing in common. What they must have in common is the list — see
/// <see cref="GuidanceActions"/>.</para></summary>
internal sealed record GuidanceAction(string Label, Action Invoke);
