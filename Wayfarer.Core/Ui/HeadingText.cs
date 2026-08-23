using System.Text;

namespace Wayfarer.Core.Ui;

/// <summary>Keeps the readout's heading to characters the game's heading font can actually draw.
///
/// <b>Why this exists.</b> The readout's heading is drawn in <c>FontType.TrumpGothic</c> — the
/// game's own HUD panel-title face, and the right choice for it. Trump Gothic is a display face with
/// a narrow glyph repertoire, and on screen the heading read "Hunting Log tt warrior" where the
/// composer had written "Hunting Log (middle dot) warrior": the middle dot is not in that font and
/// came out as something else entirely. Body text is unaffected — that is AXIS, which carries the
/// full repertoire — so this is applied to headings only, and quest and monster names keep the
/// game's own spelling.
///
/// The rule is therefore: a heading is built from ASCII. Anything typographic that gets into one
/// (through a data sheet, a translation, or a future edit here) is folded down to its plain
/// equivalent rather than gambling on the font. The mappings below are written as escapes on
/// purpose: a table of typographic characters is exactly the thing a re-encoding would corrupt, and
/// corrupting it would quietly stop it from catching anything.</summary>
public static class HeadingText
{
    /// <summary>Folds typographic punctuation down to ASCII and drops anything else non-ASCII. A
    /// heading that folds away to nothing is returned unchanged instead — a suspect glyph is easier
    /// to notice and report than a mode indicator that silently vanished.</summary>
    public static string Plain(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            Append(builder, c);
        }

        var plain = builder.ToString().Trim();
        return plain.Length == 0 ? value : plain;
    }

    private static void Append(StringBuilder builder, char c)
    {
        switch (c)
        {
            // en dash, em dash, minus sign, middle dot, bullet, katakana middle dot
            case '\u2013' or '\u2014' or '\u2212' or '\u00B7' or '\u2022' or '\u30FB':
                builder.Append('-');
                break;

            // curly quotes and the modifier apostrophe
            case '\u2018' or '\u2019' or '\u02BC':
                builder.Append('\'');
                break;

            case '\u201C' or '\u201D':
                builder.Append('"');
                break;

            case '\u2026':
                builder.Append("...");
                break;

            case '\u00D7':
                builder.Append('x');
                break;

            // non-breaking space
            case '\u00A0':
                builder.Append(' ');
                break;

            default:
                if (c is >= ' ' and <= '~')
                {
                    builder.Append(c);
                }

                break;
        }
    }
}
