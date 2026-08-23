using System.Text;

namespace Wayfarer.Core.Unlocks;

/// <summary>Folds a quest name — from the game's Quest sheet or from the catalogue — into the
/// key both sides are matched on.
///
/// <para>The Quest sheet prefixes 1,228 of its 5,356 named rows with a private-use journal icon
/// glyph (<c>U+E0BE</c>/<c>U+E0BF</c>) and a space, so the raw <c>ExtractText()</c> string never
/// equals the catalogue's plain-text name: every Aether Current and every Allied Society unlock
/// quest from Shadowbringers on failed to match for that reason alone.</para>
///
/// <para>Exactly the folds below were measured over all 5,356 names and introduce <b>zero</b> new
/// key collisions. The following were measured and rejected, and must not be added: dropping
/// parenthetical suffixes (merges the ten <c>A Relic Reborn (...)</c> rows, every Grand Company
/// triple and every housing triple — 17 new colliding groups), dropping a leading "The"
/// (<c>Dancing King</c> and <c>The Dancing King</c> are different quests), truncating at ':'
/// (merges <c>The First Stela: Of Ronkan Might</c> with <c>... Benevolence</c>), and stripping
/// all non-alphanumerics (merges <c>What's in a Name</c> with <c>What's in a Name?</c>).</para></summary>
public static class QuestNameKey
{
    /// <summary>The one sheet-side suffix that is folded away: a handful of allied-society unlock
    /// rows carry a literal <c>(way)</c> authoring marker the catalogue never reproduces
    /// (<c>" Must Be Dreaming(way)"</c>). Measured over the whole sheet, removing it introduces no
    /// collision. This is a single literal, not the general parenthetical strip that the
    /// name-reconciliation audit proves harmful.</summary>
    private const string AuthoringSuffix = "(way)";

    public static string For(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var folded = FoldPunctuation(FoldGlyphs(name.Normalize(NormalizationForm.FormKC)));
        var collapsed = CollapseWhitespace(folded);
        if (collapsed.EndsWith(AuthoringSuffix, StringComparison.Ordinal))
        {
            collapsed = collapsed[..^AuthoringSuffix.Length].TrimEnd();
        }

        return collapsed.ToLowerInvariant();
    }

    /// <summary>The same name with the journal icon glyph and the invisibles taken off, but its
    /// capitalisation and punctuation left alone — for showing to the player. Lock reasons name
    /// prerequisite quests straight from the sheet, and without this they read
    /// "needs quest '<c>[glyph]</c> Fugitive of Fear'".</summary>
    public static string Display(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var collapsed = CollapseWhitespace(FoldGlyphs(name));
        return collapsed.EndsWith(AuthoringSuffix, StringComparison.Ordinal)
            ? collapsed[..^AuthoringSuffix.Length].TrimEnd()
            : collapsed;
    }

    /// <summary>Private-use journal icon glyphs become a separator (so the following word doesn't
    /// fuse onto the previous one), and the zero-width and invisible characters that survive a
    /// copy-paste out of a wiki page are dropped — one catalogue entry shipped with a trailing
    /// <c>U+200E</c> LEFT-TO-RIGHT MARK. Nothing here changes a character the player can see, so
    /// it is safe for display as well as for keying.</summary>
    private static string FoldGlyphs(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            switch (c)
            {
                case >= '\uE000' and <= '\uF8FF':
                    sb.Append(' ');
                    break;
                case '\u200B' or '\u200C' or '\u200D' or '\u200E' or '\u200F' or '\uFEFF' or '\u00AD':
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>The punctuation the two sources disagree on — curly and modifier apostrophes,
    /// curly quotes, and the whole dash family including the minus sign — collapses onto its ASCII
    /// form. Key-only: unlike <see cref="FoldGlyphs"/> this does change what a player would read.</summary>
    private static string FoldPunctuation(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(c switch
            {
                '\u2018' or '\u2019' or '\u02BC' or '\u00B4' or '`' => '\'',
                '\u201C' or '\u201D' => '"',
                (>= '\u2010' and <= '\u2015') or '\u2212' => '-',
                _ => c,
            });
        }

        return sb.ToString();
    }

    /// <summary>Runs of whitespace become one space and leading/trailing whitespace goes — the
    /// icon glyph always leaves one behind at the front.</summary>
    private static string CollapseWhitespace(string name)
    {
        var sb = new StringBuilder(name.Length);
        var pendingSpace = false;
        foreach (var c in name)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
