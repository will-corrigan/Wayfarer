using System.Text;

namespace Wayfarer.Core.Ui;

/// <summary>Presentation-casing for names that come out of the game's data sheets.
///
/// The <c>BNpcName</c> sheet stores monster names in lower case ("dragonfly", "wharf rat") and the
/// game's own Hunting Log title-cases them at draw time. Wayfarer showed the raw sheet text, so its
/// rows, readout and info-bar entry all read as lower case beside a game window that does not. This
/// is the same transform the game applies, and it lives in Core so it is testable rather than
/// eyeballed.
///
/// Words are split on spaces only, deliberately: an apostrophe is part of the word, so
/// "coeurl's whisker" becomes "Coeurl's Whisker" and never "Coeurl'S Whisker". A word that already
/// carries a capital anywhere is left exactly as the sheet wrote it — that is what protects proper
/// nouns and numerals the sheet has already cased ("Ked", "IIIrd Cohort"). Short joining words stay
/// lower case unless they open or close the name, which is what makes "apkallu of paradise" read as
/// "Apkallu of Paradise".</summary>
public static class DisplayNames
{
    private static readonly HashSet<string> JoiningWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "but", "by", "for", "from", "in", "nor", "of", "on", "or",
        "the", "to", "with",
    };

    /// <summary>Title-cases <paramref name="value"/> for display. Null, empty and whitespace-only
    /// input is returned unchanged so callers can pass sheet text through without a guard.</summary>
    public static string TitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var words = value.Split(' ');
        var lastWord = LastNonEmpty(words);
        var firstWord = FirstNonEmpty(words);
        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(CaseWord(words[i], i == firstWord || i == lastWord));
        }

        return builder.ToString();
    }

    private static int FirstNonEmpty(string[] words)
    {
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastNonEmpty(string[] words)
    {
        for (var i = words.Length - 1; i >= 0; i--)
        {
            if (words[i].Length > 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static string CaseWord(string word, bool isEdgeWord)
    {
        if (word.Length == 0 || HasCapital(word))
        {
            return word;
        }

        if (!isEdgeWord && JoiningWords.Contains(word))
        {
            return word;
        }

        var upper = char.ToUpperInvariant(word[0]);
        return upper == word[0] ? word : string.Concat(upper.ToString(), word.AsSpan(1));
    }

    private static bool HasCapital(string word)
    {
        foreach (var c in word)
        {
            if (char.IsUpper(c))
            {
                return true;
            }
        }

        return false;
    }
}
