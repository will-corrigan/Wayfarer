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

        var folded = FoldCharacters(name.Normalize(NormalizationForm.FormKC));
        var collapsed = CollapseWhitespace(folded);
        if (collapsed.EndsWith(AuthoringSuffix, StringComparison.Ordinal))
        {
            collapsed = collapsed[..^AuthoringSuffix.Length].TrimEnd();
        }

        return collapsed.ToLowerInvariant();
    }

    /// <summary>One pass over the string doing three folds: private-use journal icon glyphs become
    /// a separator (so the following word doesn't fuse onto the previous one); zero-width and
    /// invisible characters that survive a copy-paste out of a wiki page are dropped (one
    /// catalogue entry shipped with a trailing <c>U+200E</c> LEFT-TO-RIGHT MARK); and the
    /// punctuation the two sources disagree on — curly and modifier apostrophes, curly quotes, and
    /// the whole dash family including the minus sign — collapses onto its ASCII form.</summary>
    private static string FoldCharacters(string name)
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
                case '\u2018' or '\u2019' or '\u02BC' or '\u00B4' or '`':
                    sb.Append('\'');
                    break;
                case '\u201C' or '\u201D':
                    sb.Append('"');
                    break;
                case (>= '\u2010' and <= '\u2015') or '\u2212':
                    sb.Append('-');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
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
