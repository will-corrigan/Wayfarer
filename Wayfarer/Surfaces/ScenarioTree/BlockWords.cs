using Dalamud.Game.Text.SeStringHandling;
using Wayfarer.Guidance;
using Wayfarer.Presentation;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>What the block's two lines show for a guidance: the sentence the app composed, the
/// words inside it that name what a press does, and the game icon that belongs in front of those
/// words. The sentences themselves are never written here — they come from <see cref="EntryWords"/>
/// and <see cref="RouteWords"/> — so this is only about how the game draws them.</summary>
internal static class BlockWords
{
    /// <summary>The step being guided to: its sentence, the words naming whatever it asks the
    /// player to use, perform or say, and the icon of that thing.</summary>
    public static LineContent? Entry(ObjectiveEntry? entry) =>
        entry is null ? null : new LineContent(
            EntryWords.Describe(entry),
            entry.Action?.Keyword,
            IconFor(entry.Action),
            Pressable: entry.Action is not null);

    /// <summary>The way there: its sentence behind the game's own mark for the kind of travel, and
    /// the leg the press is about.</summary>
    public static LineContent? Route(RouteLine? line) =>
        line is null ? null : new LineContent(
            line.Text,
            line.Keyword,
            Glyph: Glyph(line.Glyph),
            Pressable: line.Press is not null);

    /// <summary>The icon of the thing an entry asks for, or none when it only asks the player to
    /// be somewhere or to say something, which no icon says better than the words do.</summary>
    private static uint? IconFor(EntryAction? action) => action switch
    {
        EntryAction.UseItem item => item.IconId,
        EntryAction.Emote emote => emote.IconId,
        _ => null,
    };

    private static BitmapFontIcon? Glyph(RouteGlyph glyph) => glyph switch
    {
        RouteGlyph.Aetheryte => BitmapFontIcon.Aetheryte,
        RouteGlyph.Duty => BitmapFontIcon.WaitingForDutyFinder,
        _ => null,
    };
}
