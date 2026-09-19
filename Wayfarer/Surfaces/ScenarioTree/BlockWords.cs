using Dalamud.Game.Text.SeStringHandling;
using Lumina.Text.ReadOnly;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Presentation;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>What the block's two lines show for a guidance: the words the app composed, the game
/// icon that belongs in front of them, and whether a press does anything. The words themselves are
/// never written here — they come from <see cref="EntryWords"/> and <see cref="RouteWords"/> — so
/// this is only about how the game draws them.</summary>
internal static class BlockWords
{
    /// <summary>The step being guided to: its words, the icon of whatever it asks the player to
    /// use or perform, and a press when there is something to press.</summary>
    public static LineContent? Entry(ObjectiveEntry? entry) =>
        entry is null ? null : new LineContent(EntryWords.Describe(entry), IconFor(entry.Action), entry.Action is not null);

    /// <summary>The way there: its words behind the game's own mark for the kind of travel, and a
    /// press when the route offers one.</summary>
    public static LineContent? Route(RouteLine? line) =>
        line is null ? null : new LineContent(WithGlyph(line), null, line.Press is not null);

    /// <summary>The icon of the thing an entry asks for, or none when it only asks the player to
    /// be somewhere.</summary>
    private static uint? IconFor(EntryAction? action) => action switch
    {
        EntryAction.UseItem item => item.IconId,
        EntryAction.Emote emote => emote.IconId,
        _ => null,
    };

    /// <summary>The route's words with the game's own font icon in front of them, when the kind of
    /// travel has one.</summary>
    private static ReadOnlySeString WithGlyph(RouteLine line) =>
        Glyph(line.Glyph) is { } icon
            ? new ReadOnlySeString(new SeStringBuilder().AddIcon(icon).AddText(line.Text).Build().Encode())
            : line.Text;

    private static BitmapFontIcon? Glyph(RouteGlyph glyph) => glyph switch
    {
        RouteGlyph.Aetheryte => BitmapFontIcon.Aetheryte,
        RouteGlyph.Duty => BitmapFontIcon.WaitingForDutyFinder,
        _ => null,
    };
}
