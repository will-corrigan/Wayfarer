namespace Wayfarer.Windows.Native;

/// <summary>One button on the detail pane. Buttons are hidden rather than disabled when they do not
/// apply — the game hides inapplicable rows too, and a greyed button with no explanation is the
/// shape of the original "nothing in here works" report.</summary>
/// <param name="Label">What the button says.</param>
/// <param name="Act">What it does.</param>
internal sealed record HubDetailAction(string Label, Action Act);
