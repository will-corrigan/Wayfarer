namespace Wayfarer.App.Modules;

/// <summary>One thing about a module the player can switch, offered to the settings window without
/// the module knowing anything about how a window is drawn. The module reads and writes its own
/// setting through the two delegates; the window only shows the name and calls them.</summary>
/// <param name="Name">What the switch is called.</param>
/// <param name="Description">One line under it saying what it does.</param>
/// <param name="Read">Whether it is on right now.</param>
/// <param name="Write">Turns it on or off, taking effect at once.</param>
internal sealed record ModuleSetting(string Name, string Description, Func<bool> Read, Action<bool> Write);
