namespace Wayfarer.Core.Ui;

/// <summary>A titled group of settings. Sections are the only structure the settings surface has —
/// no nesting, no collapsibles: KamiToolKit's collapsing header has no navigation implementation at
/// all (<c>OnRecalculateNavigation</c> is an empty method with a "not implemented yet" comment), so
/// a collapsible section would be unreachable on a controller.</summary>
/// <param name="Title">The heading, in the game's own sentence case.</param>
/// <param name="Settings">The settings in this section, in display order.</param>
public sealed record SettingSection(string Title, IReadOnlyList<SettingDefinition> Settings);
