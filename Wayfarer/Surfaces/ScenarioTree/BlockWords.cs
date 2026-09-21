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
    /// <summary>The step being guided to: its sentence, and the words inside it naming whatever it
    /// asks the player to use, perform or say.</summary>
    public static LineContent? Entry(ObjectiveEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        var (opens, closes) = Marks(entry.Action);
        return new LineContent(
            EntryWords.Describe(entry),
            entry.Action?.Keyword,
            Opens: opens,
            Closes: closes,
            Pressable: entry.Action is not null);
    }

    /// <summary>The way there: its sentence behind the game's own mark for the kind of travel, and
    /// the leg the press is about.</summary>
    public static LineContent? Route(RouteLine? line) =>
        line is null ? null : new LineContent(
            line.Text,
            line.Keyword,
            Glyph: Glyph(line.Glyph),
            Pressable: line.Press is not null);

    /// <summary>The game's own marks set around the words a press is about, where the font has one
    /// that says what the press is. A phrase to be said is bracketed the way the game brackets an
    /// auto-translated one; a key item carries the star the game marks key items with. Nothing is
    /// invented: an action the font has no mark for gets none.</summary>
    private static (BitmapFontIcon? Opens, BitmapFontIcon? Closes) Marks(EntryAction? action) => action switch
    {
        EntryAction.Say => (BitmapFontIcon.AutoTranslateBegin, BitmapFontIcon.AutoTranslateEnd),
        EntryAction.UseItem { KeyItem: true } => (BitmapFontIcon.GoldStar, null),
        _ => (null, null),
    };

    private static BitmapFontIcon? Glyph(RouteGlyph glyph) => glyph switch
    {
        RouteGlyph.Aetheryte => BitmapFontIcon.Aetheryte,
        RouteGlyph.Duty => BitmapFontIcon.WaitingForDutyFinder,
        _ => null,
    };
}
